// Synthypia VST3 host bridge.
// Loads a VST3 instrument, feeds it MIDI and renders stereo audio. Exposes a small C API for P/Invoke.

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <algorithm>
#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

#include "pluginterfaces/base/ustring.h"
#include "pluginterfaces/gui/iplugview.h"
#include "pluginterfaces/vst/ivstaudioprocessor.h"
#include "pluginterfaces/vst/ivstcomponent.h"
#include "pluginterfaces/vst/ivsteditcontroller.h"
#include "pluginterfaces/vst/ivstevents.h"
#include "pluginterfaces/vst/ivstmidicontrollers.h"
#include "pluginterfaces/vst/ivstparameterchanges.h"
#include "pluginterfaces/vst/vstspeaker.h"
#include "public.sdk/source/common/memorystream.h"
#include "public.sdk/source/vst/hosting/eventlist.h"
#include "public.sdk/source/vst/hosting/hostclasses.h"
#include "public.sdk/source/vst/hosting/module.h"
#include "public.sdk/source/vst/hosting/parameterchanges.h"
#include "public.sdk/source/vst/hosting/plugprovider.h"
#include "public.sdk/source/vst/hosting/processdata.h"

using namespace Steinberg;
using namespace Steinberg::Vst;

namespace {

std::string ToUtf8(const wchar_t* w)
{
	int n = WideCharToMultiByte(CP_UTF8, 0, w, -1, nullptr, 0, nullptr, nullptr);
	std::string s(n > 0 ? n - 1 : 0, '\0');
	if (n > 1) WideCharToMultiByte(CP_UTF8, 0, w, -1, &s[0], n, nullptr, nullptr);
	return s;
}

// Der Host hält die Plugin-Seite der Parameter-Änderungen (Bedienung im Editor) fest und reicht sie an den Prozessor weiter.
class ComponentHandler : public IComponentHandler
{
public:
	tresult PLUGIN_API beginEdit(ParamID) override { return kResultOk; }
	tresult PLUGIN_API performEdit(ParamID id, ParamValue value) override
	{
		std::lock_guard<std::mutex> g(lock);
		edits.push_back({id, value});
		return kResultOk;
	}
	tresult PLUGIN_API endEdit(ParamID) override { return kResultOk; }
	tresult PLUGIN_API restartComponent(int32) override { return kResultOk; }

	tresult PLUGIN_API queryInterface(const TUID iid, void** obj) override
	{
		if (FUnknownPrivate::iidEqual(iid, IComponentHandler::iid) || FUnknownPrivate::iidEqual(iid, FUnknown::iid))
		{
			*obj = this;
			return kResultOk;
		}
		*obj = nullptr;
		return kNoInterface;
	}
	uint32 PLUGIN_API addRef() override { return 1; }
	uint32 PLUGIN_API release() override { return 1; }

	struct Edit { ParamID id; ParamValue value; };
	std::mutex lock;
	std::vector<Edit> edits;
};

typedef void (*ResizeCallback)(int w, int h);

class PlugFrame : public IPlugFrame
{
public:
	tresult PLUGIN_API resizeView(IPlugView* view, ViewRect* r) override
	{
		if (!r) return kInvalidArgument;
		if (view) view->onSize(r);
		if (callback) callback(r->right - r->left, r->bottom - r->top);
		return kResultOk;
	}
	tresult PLUGIN_API queryInterface(const TUID iid, void** obj) override
	{
		if (FUnknownPrivate::iidEqual(iid, IPlugFrame::iid) || FUnknownPrivate::iidEqual(iid, FUnknown::iid))
		{
			*obj = this;
			return kResultOk;
		}
		*obj = nullptr;
		return kNoInterface;
	}
	uint32 PLUGIN_API addRef() override { return 1; }
	uint32 PLUGIN_API release() override { return 1; }

	ResizeCallback callback = nullptr;
};

struct MidiMsg { int status, d1, d2; };

struct Host
{
	std::shared_ptr<VST3::Hosting::Module> module;
	IPtr<HostApplication> hostApp;
	IPtr<PlugProvider> provider;
	IPtr<IComponent> component;
	IPtr<IEditController> controller;
	FUnknownPtr<IAudioProcessor> processor;
	FUnknownPtr<IMidiMapping> midiMapping;
	IPtr<IPlugView> view;
	ComponentHandler handler;
	PlugFrame frame;

	HostProcessData data;
	EventList events;
	ParameterChanges inChanges;
	ParameterChanges outChanges;
	ProcessContext context {};
	std::vector<float> bufL, bufR;

	std::mutex midiLock;
	std::vector<MidiMsg> midiQueue;
	bool noteActive[16][128] {};
	bool processing = false;
	bool active = false;
	int blockSize = 256;
	std::string name;
	std::string error;
};

void SetError(char* dst, int cap, const std::string& s)
{
	if (!dst || cap <= 0) return;
	strncpy_s(dst, cap, s.c_str(), _TRUNCATE);
}

void AddParam(Host* h, ParamID id, ParamValue value)
{
	int32 index = 0;
	if (auto* q = h->inChanges.addParameterData(id, index))
	{
		int32 pt = 0;
		q->addPoint(0, value, pt);
	}
}

void AddNote(Host* h, bool on, int ch, int pitch, int vel)
{
	Event e {};
	e.busIndex = 0;
	e.sampleOffset = 0;
	e.ppqPosition = 0;
	e.flags = Event::kIsLive;
	if (on)
	{
		e.type = Event::kNoteOnEvent;
		e.noteOn.channel = (int16)ch;
		e.noteOn.pitch = (int16)pitch;
		e.noteOn.velocity = vel / 127.f;
		e.noteOn.length = 0;
		e.noteOn.tuning = 0;
		e.noteOn.noteId = -1;
	}
	else
	{
		e.type = Event::kNoteOffEvent;
		e.noteOff.channel = (int16)ch;
		e.noteOff.pitch = (int16)pitch;
		e.noteOff.velocity = vel / 127.f;
		e.noteOff.noteId = -1;
		e.noteOff.tuning = 0;
	}
	h->events.addEvent(e);
}

void ApplyMidi(Host* h, const MidiMsg& m)
{
	int hi = m.status & 0xF0, ch = m.status & 0x0F;
	if (hi == 0x90 && m.d2 > 0)
	{
		h->noteActive[ch][m.d1 & 0x7F] = true;
		AddNote(h, true, ch, m.d1, m.d2);
	}
	else if (hi == 0x80 || hi == 0x90)
	{
		h->noteActive[ch][m.d1 & 0x7F] = false;
		AddNote(h, false, ch, m.d1, m.d2);
	}
	else if (hi == 0xB0)
	{
		if (m.d1 == 120 || m.d1 == 123) // All Sound Off / All Notes Off
		{
			for (int p = 0; p < 128; p++)
				if (h->noteActive[ch][p]) { h->noteActive[ch][p] = false; AddNote(h, false, ch, p, 0); }
			return;
		}
		ParamID id;
		if (h->midiMapping && h->midiMapping->getMidiControllerAssignment(0, (int16)ch, (CtrlNumber)m.d1, id) == kResultOk)
			AddParam(h, id, m.d2 / 127.0);
	}
	else if (hi == 0xE0)
	{
		ParamID id;
		if (h->midiMapping && h->midiMapping->getMidiControllerAssignment(0, (int16)ch, kPitchBend, id) == kResultOk)
			AddParam(h, id, (((m.d2 & 0x7F) << 7) | (m.d1 & 0x7F)) / 16383.0);
	}
}

} // namespace

#define API extern "C" __declspec(dllexport)

API void* vst3_load(const wchar_t* path, int sampleRate, int blockSize, char* err, int errCap)
{
	std::unique_ptr<Host> h(new Host());
	h->blockSize = blockSize;

	std::string error;
	h->module = VST3::Hosting::Module::create(ToUtf8(path), error);
	if (!h->module)
	{
		SetError(err, errCap, "Das Modul konnte nicht geladen werden: " + error);
		return nullptr;
	}

	const auto& factory = h->module->getFactory();
	const auto infos = factory.classInfos();
	const VST3::Hosting::ClassInfo* chosen = nullptr;
	bool anyEffectClass = false;
	for (auto& ci : infos)
	{
		if (ci.category() != kVstAudioEffectClass) continue;
		anyEffectClass = true;
		if (ci.subCategoriesString().find("Instrument") != std::string::npos) { chosen = &ci; break; }
	}
	if (!chosen)
	{
		SetError(err, errCap, anyEffectClass ? u8"Das Plugin ist kein Instrument. Bitte ein Instrument-Plugin wählen."
		                                     : u8"Die Datei enthält kein VST3-Audiomodul.");
		return nullptr;
	}
	h->name = chosen->name();

	h->hostApp = owned(new HostApplication());
	PluginContextFactory::instance().setPluginContext(h->hostApp.get());
	h->provider = owned(new PlugProvider(factory, *chosen, true));
	if (!h->provider->initialize())
	{
		SetError(err, errCap, "Das Plugin konnte nicht initialisiert werden (Lizenz oder Installation prüfen).");
		return nullptr;
	}
	h->component = h->provider->getComponentPtr();
	h->controller = h->provider->getControllerPtr();
	h->processor = FUnknownPtr<IAudioProcessor>(h->component);
	if (!h->processor)
	{
		SetError(err, errCap, "Das Plugin liefert keinen Audio-Prozessor.");
		return nullptr;
	}

	if (h->controller)
	{
		h->controller->setComponentHandler(&h->handler);
		h->midiMapping = FUnknownPtr<IMidiMapping>(h->controller);

		// Zustand des Prozessors an den Controller übergeben (nötig bei getrennten Komponenten)
		auto* stream = new MemoryStream();
		IPtr<IBStream> s = owned(static_cast<IBStream*>(stream));
		if (h->component->getState(s) == kResultTrue)
		{
			int64 pos;
			s->seek(0, IBStream::kIBSeekSet, &pos);
			h->controller->setComponentState(s);
		}
	}

	// Busse: Stereo-Ausgang, Eingänge aus, Event-Eingang an
	int32 nOut = h->component->getBusCount(kAudio, kOutput);
	int32 nIn = h->component->getBusCount(kAudio, kInput);
	std::vector<SpeakerArrangement> ins(nIn, SpeakerArr::kStereo), outs(nOut, SpeakerArr::kStereo);
	h->processor->setBusArrangements(nIn ? ins.data() : nullptr, nIn, nOut ? outs.data() : nullptr, nOut);
	if (nOut > 0) h->component->activateBus(kAudio, kOutput, 0, true);
	if (h->component->getBusCount(kEvent, kInput) > 0) h->component->activateBus(kEvent, kInput, 0, true);

	ProcessSetup setup {};
	setup.processMode = kRealtime;
	setup.symbolicSampleSize = kSample32;
	setup.maxSamplesPerBlock = blockSize;
	setup.sampleRate = sampleRate;
	if (h->processor->setupProcessing(setup) != kResultOk)
	{
		SetError(err, errCap, "Das Plugin akzeptiert die Audio-Einstellungen nicht.");
		return nullptr;
	}

	if (h->component->setActive(true) != kResultOk) { SetError(err, errCap, "Das Plugin ließ sich nicht aktivieren."); return nullptr; }
	h->active = true;
	h->processor->setProcessing(true);
	h->processing = true;

	if (!h->data.prepare(*h->component, blockSize, kSample32))
	{
		SetError(err, errCap, "Audio-Puffer konnten nicht angelegt werden.");
		return nullptr;
	}
	h->data.processContext = &h->context;
	h->data.inputEvents = &h->events;
	h->data.inputParameterChanges = &h->inChanges;
	h->data.outputParameterChanges = &h->outChanges;
	h->data.numSamples = blockSize;
	h->data.processMode = kRealtime;

	h->context.sampleRate = sampleRate;
	h->context.tempo = 120.0;
	h->context.timeSigNumerator = 4;
	h->context.timeSigDenominator = 4;
	h->context.state = ProcessContext::kPlaying | ProcessContext::kTempoValid | ProcessContext::kTimeSigValid;

	return h.release();
}

API void vst3_destroy(void* handle)
{
	Host* h = static_cast<Host*>(handle);
	if (!h) return;
	if (h->view) { h->view->removed(); h->view->setFrame(nullptr); h->view = nullptr; }
	if (h->processing) h->processor->setProcessing(false);
	if (h->active) h->component->setActive(false);
	if (h->controller) h->controller->setComponentHandler(nullptr);
	h->data.unprepare();
	h->midiMapping = nullptr;
	h->processor = nullptr;
	// PlugProvider::~PlugProvider beendet Komponente und Controller
	h->component = nullptr;
	h->controller = nullptr;
	h->provider = nullptr;
	h->hostApp = nullptr;
	PluginContextFactory::instance().setPluginContext(nullptr);
	delete h;
}

API const char* vst3_name(void* handle) { return static_cast<Host*>(handle)->name.c_str(); }

API void vst3_midi(void* handle, int status, int d1, int d2)
{
	Host* h = static_cast<Host*>(handle);
	std::lock_guard<std::mutex> g(h->midiLock);
	if (h->midiQueue.size() < 4096) h->midiQueue.push_back({status, d1, d2});
}

// Rendert blockSize Frames in links/rechts.
API void vst3_process(void* handle, float* left, float* right, int frames)
{
	Host* h = static_cast<Host*>(handle);
	if (frames > h->blockSize) frames = h->blockSize;

	std::vector<MidiMsg> msgs;
	{
		std::lock_guard<std::mutex> g(h->midiLock);
		msgs.swap(h->midiQueue);
	}
	h->events.clear();
	h->inChanges.clearQueue();
	h->outChanges.clearQueue();
	for (auto& m : msgs) ApplyMidi(h, m);
	{
		std::lock_guard<std::mutex> g(h->handler.lock);
		for (auto& e : h->handler.edits) AddParam(h, e.id, e.value);
		h->handler.edits.clear();
	}

	h->data.numSamples = frames;
	if (h->data.numOutputs > 0 && h->data.outputs)
	{
		AudioBusBuffers& out = h->data.outputs[0];
		for (int c = 0; c < out.numChannels; c++)
			if (out.channelBuffers32[c]) std::memset(out.channelBuffers32[c], 0, sizeof(float) * frames);
	}

	h->processor->process(h->data);
	h->context.continousTimeSamples += frames;
	h->context.projectTimeSamples += frames;

	std::memset(left, 0, sizeof(float) * frames);
	std::memset(right, 0, sizeof(float) * frames);
	if (h->data.numOutputs > 0 && h->data.outputs)
	{
		AudioBusBuffers& out = h->data.outputs[0];
		if (out.numChannels > 0 && out.channelBuffers32[0])
			std::memcpy(left, out.channelBuffers32[0], sizeof(float) * frames);
		if (out.numChannels > 1 && out.channelBuffers32[1])
			std::memcpy(right, out.channelBuffers32[1], sizeof(float) * frames);
		else
			std::memcpy(right, left, sizeof(float) * frames);
	}
}

// ---------------------------------------------------------------- Zustand (Presets, geladene Instrumente)

namespace {

void AppendInt(std::vector<unsigned char>& v, int x)
{
	unsigned char b[4];
	std::memcpy(b, &x, 4);
	v.insert(v.end(), b, b + 4);
}

void AppendStream(std::vector<unsigned char>& v, MemoryStream& s)
{
	int64 pos = 0;
	s.tell(&pos);
	AppendInt(v, (int)pos);
	if (pos > 0) v.insert(v.end(), (unsigned char*)s.getData(), (unsigned char*)s.getData() + pos);
}

} // namespace

// Schreibt [Größe Komponente][Daten][Größe Controller][Daten] nach buf. Gibt die benötigte Größe zurück (0 = kein Zustand).
API int vst3_get_state(void* handle, unsigned char* buf, int cap)
{
	Host* h = static_cast<Host*>(handle);
	std::vector<unsigned char> out;

	IPtr<MemoryStream> comp = owned(new MemoryStream());
	if (h->component->getState(comp) != kResultTrue) return 0;
	AppendStream(out, *comp);

	IPtr<MemoryStream> ctrl = owned(new MemoryStream());
	if (h->controller && h->controller->getState(ctrl) == kResultTrue) AppendStream(out, *ctrl);
	else AppendInt(out, 0);

	if (buf && cap >= (int)out.size()) std::memcpy(buf, out.data(), out.size());
	return (int)out.size();
}

API int vst3_set_state(void* handle, const unsigned char* buf, int size)
{
	Host* h = static_cast<Host*>(handle);
	if (!buf || size < 8) return 0;

	int compSize = 0;
	std::memcpy(&compSize, buf, 4);
	if (compSize < 0 || 4 + compSize + 4 > size) return 0;
	const unsigned char* compData = buf + 4;

	int ctrlSize = 0;
	std::memcpy(&ctrlSize, buf + 4 + compSize, 4);
	if (ctrlSize < 0 || 4 + compSize + 4 + ctrlSize > size) return 0;
	const unsigned char* ctrlData = buf + 4 + compSize + 4;

	int ok = 0;
	if (compSize > 0)
	{
		IPtr<MemoryStream> comp = owned(new MemoryStream((void*)compData, compSize));
		if (h->component->setState(comp) == kResultOk) ok |= 1;
		if (h->controller)
		{
			IPtr<MemoryStream> comp2 = owned(new MemoryStream((void*)compData, compSize));
			if (h->controller->setComponentState(comp2) == kResultOk) ok |= 2;
		}
	}
	if (ctrlSize > 0 && h->controller)
	{
		IPtr<MemoryStream> ctrl = owned(new MemoryStream((void*)ctrlData, ctrlSize));
		if (h->controller->setState(ctrl) == kResultOk) ok |= 4;
	}
	return ok;
}

// ---------------------------------------------------------------- Editor (nur im UI-Thread aufrufen)

API int vst3_has_editor(void* handle)
{
	Host* h = static_cast<Host*>(handle);
	if (!h->controller) return 0;
	IPtr<IPlugView> v = owned(h->controller->createView(ViewType::kEditor));
	return v ? 1 : 0;
}

API int vst3_editor_open(void* handle, void* hwnd, ResizeCallback onResize, int* width, int* height)
{
	Host* h = static_cast<Host*>(handle);
	if (!h->controller || h->view) return 0;
	h->view = owned(h->controller->createView(ViewType::kEditor));
	if (!h->view) return 0;
	if (h->view->isPlatformTypeSupported(kPlatformTypeHWND) != kResultTrue) { h->view = nullptr; return 0; }
	h->frame.callback = onResize;
	h->view->setFrame(&h->frame);
	ViewRect r {};
	if (h->view->getSize(&r) == kResultOk)
	{
		if (width) *width = r.right - r.left;
		if (height) *height = r.bottom - r.top;
	}
	if (h->view->attached(hwnd, kPlatformTypeHWND) != kResultOk)
	{
		h->view->setFrame(nullptr);
		h->view = nullptr;
		return 0;
	}
	return 1;
}

API void vst3_editor_close(void* handle)
{
	Host* h = static_cast<Host*>(handle);
	if (!h->view) return;
	h->view->removed();
	h->view->setFrame(nullptr);
	h->view = nullptr;
}

API void vst3_editor_size(void* handle, int w, int hgt)
{
	Host* h = static_cast<Host*>(handle);
	if (!h->view) return;
	ViewRect r {0, 0, w, hgt};
	h->view->onSize(&r);
}
