/*  kainosvst.cpp

This file is part of Kainos, a program that implements a Software-Defined Radio.

Copyright (C) 2026 Justin Cron K7JUS

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

// Kainos's VST3 host: a plugin as a stage of the Kainos Audio chain (WDSP aetherstrip, AS_PLUGIN). Built on
// Steinberg's VST3 SDK hosting classes (MIT licence). The console loads a plugin here, and hands kv_process and
// the instance to WDSP, which calls it at the plugin's slot in the chain on the DSP thread; the console opens the
// plugin's own editor in a window of its own (kv_editor_open). Parameter changes made in the editor reach the
// audio thread through the SDK's lock-free ParameterChangeTransfer.

#include "public.sdk/source/vst/hosting/module.h"
#include "public.sdk/source/vst/hosting/plugprovider.h"
#include "public.sdk/source/vst/hosting/hostclasses.h"
#include "public.sdk/source/vst/hosting/processdata.h"
#include "public.sdk/source/vst/hosting/parameterchanges.h"
#include "public.sdk/source/vst/hosting/eventlist.h"
#include "public.sdk/source/vst/utility/stringconvert.h"
#include "pluginterfaces/vst/ivstaudioprocessor.h"
#include "pluginterfaces/vst/ivsteditcontroller.h"
#include "pluginterfaces/vst/ivstcomponent.h"
#include "pluginterfaces/gui/iplugview.h"
#include "pluginterfaces/gui/iplugviewcontentscalesupport.h"
#include "base/source/fobject.h"
#include "public.sdk/source/common/memorystream.h"

#include <windows.h>
#include <string>
#include <vector>
#include <mutex>
#include <atomic>
#include <cstring>

using namespace Steinberg;
using namespace Steinberg::Vst;

namespace Steinberg { FUnknown* gStandardPluginContext = nullptr; }

#define KV_API extern "C" __declspec(dllexport)

namespace
{
	HostApplication* gHost = nullptr;

	void ensureHost ()
	{
		if (gHost) return;
		gHost = new HostApplication ();
		gStandardPluginContext = gHost;
		PluginContextFactory::instance ().setPluginContext (gHost);
	}

	std::string narrow (const wchar_t* w)
	{
		if (!w) return std::string ();
		int n = WideCharToMultiByte (CP_UTF8, 0, w, -1, nullptr, 0, nullptr, nullptr);
		std::string s (n > 0 ? n - 1 : 0, '\0');
		if (n > 1) WideCharToMultiByte (CP_UTF8, 0, w, -1, &s[0], n, nullptr, nullptr);
		return s;
	}

	void copyOut (const std::string& s, wchar_t* buf, int cap)
	{
		if (!buf || cap <= 0) return;
		int n = MultiByteToWideChar (CP_UTF8, 0, s.c_str (), -1, buf, cap);
		if (n == 0) buf[cap - 1] = 0;
	}

	// the editor's parameter changes, on their way to the audio thread
	class ComponentHandler : public IComponentHandler
	{
	public:
		ParameterChangeTransfer* transfer = nullptr;
		IEditController* controller = nullptr;
		std::atomic<int> edits { 0 };		// counts changes made in the editor (the console saves the state after them)

		tresult PLUGIN_API beginEdit (ParamID) override { return kResultOk; }
		tresult PLUGIN_API performEdit (ParamID id, ParamValue value) override
		{
			if (transfer) transfer->addChange (id, value, 0);
			edits++;
			return kResultOk;
		}
		tresult PLUGIN_API endEdit (ParamID) override { edits++; return kResultOk; }
		tresult PLUGIN_API restartComponent (int32) override { edits++; return kResultOk; }

		tresult PLUGIN_API queryInterface (const TUID iid, void** obj) override
		{
			if (FUnknownPrivate::iidEqual (iid, IComponentHandler::iid) || FUnknownPrivate::iidEqual (iid, FUnknown::iid))
			{
				*obj = this;
				return kResultOk;
			}
			*obj = nullptr;
			return kNoInterface;
		}
		uint32 PLUGIN_API addRef () override { return 1; }
		uint32 PLUGIN_API release () override { return 1; }
	};

	// the plugin's editor asks for a new size: kept for the console to follow (kv_editor_size)
	class PlugFrame : public IPlugFrame
	{
	public:
		std::atomic<int> w { 0 }, h { 0 };
		HWND parent = nullptr;

		tresult PLUGIN_API resizeView (IPlugView* view, ViewRect* r) override
		{
			if (!r || !view) return kInvalidArgument;
			w = r->getWidth ();
			h = r->getHeight ();
			if (parent) SetWindowPos (parent, nullptr, 0, 0, w, h, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
			view->onSize (r);
			return kResultOk;
		}
		tresult PLUGIN_API queryInterface (const TUID iid, void** obj) override
		{
			if (FUnknownPrivate::iidEqual (iid, IPlugFrame::iid) || FUnknownPrivate::iidEqual (iid, FUnknown::iid))
			{
				*obj = this;
				return kResultOk;
			}
			*obj = nullptr;
			return kNoInterface;
		}
		uint32 PLUGIN_API addRef () override { return 1; }
		uint32 PLUGIN_API release () override { return 1; }
	};

	struct Instance
	{
		VST3::Hosting::Module::Ptr module;
		IPtr<PlugProvider> provider;
		IPtr<IComponent> component;
		IPtr<IEditController> controller;
		FUnknownPtr<IAudioProcessor> processor;
		HostProcessData data;
		ParameterChanges inputChanges;
		ParameterChangeTransfer transfer { 1000 };
		EventList events;
		ProcessContext context {};
		ComponentHandler handler;
		PlugFrame frame;
		IPtr<IPlugView> view;
		std::string name;
		double rate = 48000;
		int maxBlock = 0;
		int inChannels = 0, outChannels = 0;
		std::vector<float> inL, inR, outL, outR;
		bool processing = false;
	};

	int busChannels (IComponent* c, BusDirection dir)
	{
		if (c->getBusCount (kAudio, dir) < 1) return 0;
		BusInfo info {};
		if (c->getBusInfo (kAudio, dir, 0, info) != kResultOk) return 0;
		return info.channelCount;
	}

	// a saved state: [int32 n][component state, n bytes][int32 m][controller state, m bytes]
	void applyState (Instance* k, const unsigned char* p, int len)
	{
		int32 n = 0, m = 0;
		std::memcpy (&n, p, 4);
		if (n < 0 || 4 + n + 4 > len) return;
		std::memcpy (&m, p + 4 + n, 4);
		if (m < 0 || 4 + n + 4 + m > len) m = 0;
		if (n > 0)
		{
			MemoryStream a ((void*)(p + 4), n);
			k->component->setState (&a);
			if (k->controller)
			{
				MemoryStream b ((void*)(p + 4), n);
				k->controller->setComponentState (&b);
			}
		}
		if (m > 0 && k->controller)
		{
			MemoryStream c ((void*)(p + 8 + n), m);
			k->controller->setState (&c);
		}
	}

	bool setup (Instance* k)
	{
		k->processor = FUnknownPtr<IAudioProcessor> (k->component);
		if (!k->processor) return false;
		// stereo in and out where the plugin will take it (mono audio goes to both sides)
		SpeakerArrangement st = SpeakerArr::kStereo, mono = SpeakerArr::kMono;
		if (k->processor->setBusArrangements (&st, 1, &st, 1) != kResultTrue)
			k->processor->setBusArrangements (&mono, 1, &mono, 1);
		ProcessSetup ps { kRealtime, kSample32, k->maxBlock, k->rate };
		if (k->processor->setupProcessing (ps) != kResultOk) return false;
		if (k->component->getBusCount (kAudio, kInput) > 0) k->component->activateBus (kAudio, kInput, 0, true);
		if (k->component->getBusCount (kAudio, kOutput) > 0) k->component->activateBus (kAudio, kOutput, 0, true);
		k->inChannels = busChannels (k->component, kInput);
		k->outChannels = busChannels (k->component, kOutput);
		if (k->outChannels < 1) return false;
		k->data.prepare (*k->component, 0, kSample32);		// 0: our buffers (inL ... outR), not ones it would own and free
		k->data.inputParameterChanges = &k->inputChanges;
		k->data.inputEvents = &k->events;
		k->data.processContext = &k->context;
		k->context.sampleRate = k->rate;
		k->context.tempo = 120;
		k->inL.assign (k->maxBlock, 0.f); k->inR.assign (k->maxBlock, 0.f);
		k->outL.assign (k->maxBlock, 0.f); k->outR.assign (k->maxBlock, 0.f);
		if (k->component->setActive (true) != kResultOk) return false;
		k->processor->setProcessing (true);
		k->processing = true;
		return true;
	}
}

// Every audio-effect plugin in a .vst3 file (or bundle): "path\tclass id\tname\n" lines into buf. Returns the
// number found, or -1 if the module wouldn't load (err says why).
KV_API int kv_scan (const wchar_t* path, wchar_t* buf, int cap)
{
	ensureHost ();
	std::string error;
	auto module = VST3::Hosting::Module::create (narrow (path), error);
	if (!module) { copyOut ("!" + error, buf, cap); return -1; }
	std::string out;
	int n = 0;
	for (auto& ci : module->getFactory ().classInfos ())
	{
		if (ci.category () != kVstAudioEffectClass) continue;
		out += narrow (path) + "\t" + ci.ID ().toString () + "\t" + ci.name () + "\n";
		n++;
	}
	copyOut (out, buf, cap);
	return n;
}

// Loads a plugin (classId empty: the first audio effect in the file), ready to process at this rate and up to
// maxBlock frames a call. Returns the instance, or null (err says why).
// state / stateLen: a saved state (kv_get_state), or none.
KV_API void* kv_load (const wchar_t* path, const wchar_t* classId, double rate, int maxBlock,
	const unsigned char* state, int stateLen, wchar_t* err, int errCap)
{
	ensureHost ();
	Instance* k = new Instance ();
	std::string error;
	k->module = VST3::Hosting::Module::create (narrow (path), error);
	if (!k->module) { copyOut (error, err, errCap); delete k; return nullptr; }
	auto factory = k->module->getFactory ();
	std::string want = narrow (classId);
	for (auto& ci : factory.classInfos ())
	{
		if (ci.category () != kVstAudioEffectClass) continue;
		if (!want.empty () && ci.ID ().toString () != want) continue;
		k->provider = owned (new PlugProvider (factory, ci, true));
		k->name = ci.name ();
		break;
	}
	if (!k->provider) { copyOut ("no audio effect in the file", err, errCap); delete k; return nullptr; }
	// creates the plugin (component and controller, connected and synchronised) with our host context
	if (!k->provider->initialize ()) { copyOut ("the plugin wouldn't start", err, errCap); delete k; return nullptr; }
	k->component = k->provider->getComponentPtr ();
	k->controller = k->provider->getControllerPtr ();
	if (!k->component) { copyOut ("the plugin wouldn't start", err, errCap); delete k; return nullptr; }
	k->rate = rate;
	k->maxBlock = maxBlock > 0 ? maxBlock : 4096;
	k->handler.transfer = &k->transfer;
	k->handler.controller = k->controller;
	if (k->controller) k->controller->setComponentHandler (&k->handler);
	if (state && stateLen > 8) applyState (k, state, stateLen);
	if (!setup (k)) { copyOut ("the plugin wouldn't set up for " + std::to_string ((int)rate) + " Hz", err, errCap); delete k; return nullptr; }
	return k;
}

// One block, in place: x holds frames frames of nch interleaved channels (1: mono, 2: left / right). Called by
// WDSP on the DSP thread at the plugin's slot in the chain.
KV_API int kv_process (void* h, float* x, int frames, int nch)
{
	Instance* k = (Instance*)h;
	if (!k || !k->processing || frames <= 0) return 0;
	for (int done = 0; done < frames; )
	{
		int n = frames - done;
		if (n > k->maxBlock) n = k->maxBlock;
		float* p = x + done * nch;
		for (int i = 0; i < n; i++)
		{
			k->inL[i] = p[i * nch];
			k->inR[i] = nch > 1 ? p[i * nch + 1] : p[i * nch];
		}
		k->data.numSamples = n;
		if (k->inChannels > 0) k->data.setChannelBuffer (kInput, 0, 0, k->inL.data ());
		if (k->inChannels > 1) k->data.setChannelBuffer (kInput, 0, 1, k->inR.data ());
		k->data.setChannelBuffer (kOutput, 0, 0, k->outL.data ());
		if (k->outChannels > 1) k->data.setChannelBuffer (kOutput, 0, 1, k->outR.data ());
		k->transfer.transferChangesTo (k->inputChanges);
		bool ok = k->processor->process (k->data) == kResultOk;
		k->inputChanges.clearQueue ();
		k->events.clear ();
		if (ok)
		{
			const float* r = k->outChannels > 1 ? k->outR.data () : k->outL.data ();
			for (int i = 0; i < n; i++)
			{
				if (nch > 1) { p[i * nch] = k->outL[i]; p[i * nch + 1] = r[i]; }
				else p[i] = 0.5f * (k->outL[i] + r[i]);
			}
		}
		k->context.continousTimeSamples += n;
		done += n;
	}
	return 1;
}

// The plugin's state (its settings), to save: the size it needs, written into buf when it fits (cap)
KV_API int kv_get_state (void* h, unsigned char* buf, int cap)
{
	Instance* k = (Instance*)h;
	if (!k || !k->component) return 0;
	MemoryStream a, c;
	if (k->component->getState (&a) != kResultOk) a.setSize (0);
	if (!k->controller || k->controller->getState (&c) != kResultOk) c.setSize (0);
	int32 n = (int32)a.getSize (), m = (int32)c.getSize ();
	int need = 8 + n + m;
	if (buf && cap >= need)
	{
		std::memcpy (buf, &n, 4);
		if (n) std::memcpy (buf + 4, a.getData (), n);
		std::memcpy (buf + 4 + n, &m, 4);
		if (m) std::memcpy (buf + 8 + n, c.getData (), m);
	}
	return need;
}

// changes made in the plugin's editor so far (the console saves the state when this moves)
KV_API int kv_edits (void* h)
{
	Instance* k = (Instance*)h;
	return k ? k->handler.edits.load () : 0;
}

// for handing to WDSP (SetTXAStripPlugin)
KV_API void* kv_process_proc () { return (void*)&kv_process; }

KV_API int kv_latency (void* h)
{
	Instance* k = (Instance*)h;
	return k && k->processor ? (int)k->processor->getLatencySamples () : 0;
}

KV_API int kv_name (void* h, wchar_t* buf, int cap)
{
	Instance* k = (Instance*)h;
	if (!k) return 0;
	copyOut (k->name, buf, cap);
	return 1;
}

// The plugin's own editor, inside the window hwnd (sized to the editor: w, h), drawn at the screen's scale
// (1.5 at 150%) where the plugin supports it. Returns 0 if it has none.
KV_API int kv_editor_open (void* h, void* hwnd, float scale, int* w, int* hh)
{
	Instance* k = (Instance*)h;
	if (!k || !k->controller || k->view) return 0;
	IPtr<IPlugView> v = owned (k->controller->createView (ViewType::kEditor));
	if (!v || v->isPlatformTypeSupported (kPlatformTypeHWND) != kResultTrue) return 0;
	k->frame.parent = (HWND)hwnd;
	v->setFrame (&k->frame);
	if (scale > 0.f)
	{
		FUnknownPtr<IPlugViewContentScaleSupport> cs (v);
		if (cs) cs->setContentScaleFactor (scale);
	}
	if (v->attached (hwnd, kPlatformTypeHWND) != kResultOk) { v->setFrame (nullptr); return 0; }
	ViewRect r;
	if (v->getSize (&r) == kResultOk)
	{
		k->frame.w = r.getWidth ();
		k->frame.h = r.getHeight ();
	}
	if (w) *w = k->frame.w;
	if (hh) *hh = k->frame.h;
	k->view = v;
	return 1;
}

// the editor's current size (it can ask for a new one)
KV_API void kv_editor_size (void* h, int* w, int* hh)
{
	Instance* k = (Instance*)h;
	if (!k) return;
	if (w) *w = k->frame.w;
	if (hh) *hh = k->frame.h;
}

KV_API void kv_editor_close (void* h)
{
	Instance* k = (Instance*)h;
	if (!k || !k->view) return;
	k->view->removed ();
	k->view->setFrame (nullptr);
	k->view = nullptr;
	k->frame.parent = nullptr;
}

// The console has taken the plugin out of the chain first (SetTXAStripPlugin with null), so nothing calls
// kv_process any more.
KV_API void kv_unload (void* h)
{
	Instance* k = (Instance*)h;
	if (!k) return;
	kv_editor_close (h);
	if (k->processing)
	{
		k->processor->setProcessing (false);
		k->component->setActive (false);
		k->processing = false;
	}
	if (k->controller) k->controller->setComponentHandler (nullptr);
	k->data.unprepare ();
	k->processor = nullptr;
	k->controller = nullptr;
	k->component = nullptr;
	k->provider = nullptr;
	k->module = nullptr;
	delete k;
}
