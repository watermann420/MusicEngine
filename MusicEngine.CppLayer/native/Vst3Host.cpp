// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngine
// Description: Minimal VST3 editor host bridge.

#if defined(_WIN32)
#include <windows.h>
#endif

#include "public.sdk/source/vst/hosting/module.h"
#include "public.sdk/source/vst/hosting/plugprovider.h"
#include "public.sdk/source/vst/hosting/hostclasses.h"
#include "public.sdk/source/vst/hosting/eventlist.h"
#include "public.sdk/source/vst/hosting/parameterchanges.h"
#include "public.sdk/source/vst/hosting/processdata.h"
#include "pluginterfaces/base/funknown.h"
#include "pluginterfaces/gui/iplugview.h"
#include "pluginterfaces/vst/ivstaudioprocessor.h"
#include "pluginterfaces/vst/ivstcomponent.h"
#include "pluginterfaces/vst/ivsteditcontroller.h"
#include "pluginterfaces/vst/ivstmidicontrollers.h"
#include "pluginterfaces/vst/vsttypes.h"
#include "public.sdk/source/vst/utility/stringconvert.h"
#include "public.sdk/source/vst/utility/memoryibstream.h"

#include <algorithm>
#include <atomic>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

using namespace Steinberg;
using namespace Steinberg::Vst;

namespace
{
class PlugFrame final : public IPlugFrame
{
public:
	explicit PlugFrame (void* hwnd) : hwnd_ (hwnd) {}

	tresult PLUGIN_API resizeView (IPlugView* view, ViewRect* newSize) override
	{
		if (!view || !newSize || !hwnd_)
			return kInvalidArgument;
		const int width = newSize->right - newSize->left;
		const int height = newSize->bottom - newSize->top;
#if defined(_WIN32)
		::SetWindowPos (static_cast<HWND> (hwnd_), nullptr, 0, 0, width, height, SWP_NOZORDER | SWP_NOMOVE);
#endif
		return kResultTrue;
	}

	void* hwnd () const { return hwnd_; }

private:
	tresult PLUGIN_API queryInterface (const TUID _iid, void** obj) override
	{
		if (FUnknownPrivate::iidEqual (_iid, IPlugFrame::iid) ||
		    FUnknownPrivate::iidEqual (_iid, FUnknown::iid))
		{
			*obj = this;
			addRef ();
			return kResultTrue;
		}
		*obj = nullptr;
		return kNoInterface;
	}
	uint32 PLUGIN_API addRef () override { return 1000; }
	uint32 PLUGIN_API release () override { return 1000; }

	void* hwnd_ {nullptr};
};

class ComponentHandler final : public IComponentHandler
{
public:
	tresult PLUGIN_API beginEdit (ParamID /*id*/) override { return kNotImplemented; }
	tresult PLUGIN_API performEdit (ParamID /*id*/, ParamValue /*valueNormalized*/) override
	{
		return kNotImplemented;
	}
	tresult PLUGIN_API endEdit (ParamID /*id*/) override { return kNotImplemented; }
	tresult PLUGIN_API restartComponent (int32 /*flags*/) override { return kNotImplemented; }

private:
	tresult PLUGIN_API queryInterface (const TUID _iid, void** obj) override
	{
		if (FUnknownPrivate::iidEqual (_iid, IComponentHandler::iid) ||
		    FUnknownPrivate::iidEqual (_iid, FUnknown::iid))
		{
			*obj = this;
			addRef ();
			return kResultTrue;
		}
		*obj = nullptr;
		return kNoInterface;
	}
	uint32 PLUGIN_API addRef () override { return 1000; }
	uint32 PLUGIN_API release () override { return 1000; }
};

struct Vst3HostInstance
{
	VST3::Hosting::Module::Ptr module;
	std::unique_ptr<PlugProvider> provider;
	IPtr<IComponent> component;
	IPtr<IEditController> controller;
	IPtr<IMidiMapping> midiMapping;
	IPtr<IPlugView> view;
	std::unique_ptr<PlugFrame> frame;
	HostApplication hostApp;
	ComponentHandler componentHandler;
	IPtr<IAudioProcessor> processor;
	HostProcessData processData;
	EventList eventList;
	ParameterChanges inputParameterChanges;
	ProcessContext processContext {};
	std::mutex eventMutex;
	std::mutex processMutex;
	std::atomic<bool> suspendProcessing {false};
	std::vector<Event> pendingEvents;
	struct PendingParam
	{
		ParamID id;
		ParamValue value;
	};
	std::vector<PendingParam> pendingParams;
	ParamID pitchParamIds[16] {};
	bool pitchParamValid[16] {};
	std::vector<Sample32> outputScratch;
	std::vector<Sample32> inputScratch;
	std::vector<Sample32*> outputChannels;
	std::vector<Sample32*> inputChannels;
	int32 outputChannelCount {0};
	int32 inputChannelCount {0};
	int32 blockSize {0};
	int64 continuousSamples {0};
	double sampleRate {0.0};
	bool processing {false};

	bool initialize (const std::string& path)
	{
		std::string error;
		module = VST3::Hosting::Module::create (path, error);
		if (!module)
			return false;

		auto factory = module->getFactory ();
		factory.setHostContext (&hostApp);
		PluginContextFactory::instance ().setPluginContext (&hostApp);

		for (auto& classInfo : factory.classInfos ())
		{
			if (classInfo.category () == kVstAudioEffectClass)
			{
				provider = std::make_unique<PlugProvider> (factory, classInfo, true);
				if (!provider->initialize ())
					provider.reset ();
				break;
			}
		}
		if (!provider)
			return false;

		component = provider->getComponentPtr ();
		controller = provider->getControllerPtr ();
		if (!controller)
			return false;

		controller->setComponentHandler (&componentHandler);
		midiMapping = U::cast<IMidiMapping> (controller);

		processor = U::cast<IAudioProcessor> (component);
		if (!processor)
			return false;

		for (int i = 0; i < 16; ++i)
		{
			pitchParamIds[i] = kNoParamId;
			pitchParamValid[i] = false;
		}

		activateBusses ();
		cacheBusChannels ();

		if (component)
			component->setActive (true);

		return true;
	}

	bool openEditor (void* hwnd)
	{
#if !defined(_WIN32)
		(void) hwnd;
		return false;
#else
		if (!controller)
			return false;

		suspendProcessing.store (true, std::memory_order_release);
		std::lock_guard<std::mutex> lock (processMutex);

		view = owned (controller->createView (ViewType::kEditor));
		if (!view)
		{
			suspendProcessing.store (false, std::memory_order_release);
			return false;
		}

		frame = std::make_unique<PlugFrame> (hwnd);
		view->setFrame (frame.get ());

		if (view->attached (static_cast<HWND> (hwnd), kPlatformTypeHWND) != kResultTrue)
		{
			suspendProcessing.store (false, std::memory_order_release);
			return false;
		}

		ViewRect size {};
		if (view->getSize (&size) == kResultTrue)
		{
			const int width = size.right - size.left;
			const int height = size.bottom - size.top;
			::SetWindowPos (static_cast<HWND> (hwnd), nullptr, 0, 0, width, height, SWP_NOZORDER | SWP_NOMOVE);
		}

		suspendProcessing.store (false, std::memory_order_release);
		return true;
#endif
	}

	void closeEditor ()
	{
		if (view)
		{
			view->removed ();
			view->setFrame (nullptr);
			view = nullptr;
		}
		frame.reset ();
		if (component)
			component->setActive (false);
		controller = nullptr;
		component = nullptr;
		provider.reset ();
		module.reset ();
	}

	bool setupAudio (int32 sampleRateHz, int32 blockSizeSamples)
	{
		std::lock_guard<std::mutex> lock (processMutex);
		if (!component || !processor)
			return false;
		if (sampleRateHz <= 0 || blockSizeSamples <= 0)
			return false;

		if (processing && sampleRate == sampleRateHz && blockSize == blockSizeSamples)
			return true;

		if (processing)
		{
			processor->setProcessing (false);
			component->setActive (false);
			processing = false;
		}

		if (!processData.prepare (*component, blockSizeSamples, kSample32))
			return false;

		processData.inputEvents = &eventList;
		processData.inputParameterChanges = &inputParameterChanges;
		processData.processContext = &processContext;
		processData.processMode = kRealtime;
		processData.symbolicSampleSize = kSample32;

		processContext = {};
		processContext.sampleRate = sampleRateHz;
		processContext.tempo = 120.0;
		processContext.state = ProcessContext::kPlaying;

		ProcessSetup setup {kRealtime, kSample32, blockSizeSamples,
		                    static_cast<SampleRate> (sampleRateHz)};
		if (processor->setupProcessing (setup) != kResultOk)
			return false;

		if (component->setActive (true) != kResultOk)
			return false;

		processor->setProcessing (true);

		sampleRate = sampleRateHz;
		blockSize = blockSizeSamples;
		processing = true;
		return true;
	}

	bool processAudio (Sample32* outputInterleaved, int32 frames, int32 channels)
	{
		if (suspendProcessing.load (std::memory_order_acquire))
			return false;
		if (!processing || !outputInterleaved || frames <= 0 || channels <= 0)
			return false;

		std::lock_guard<std::mutex> lock (processMutex);
		ensureScratchBuffers (frames);
		std::fill (outputScratch.begin (), outputScratch.end (), 0.0f);
		if (!inputScratch.empty ())
			std::fill (inputScratch.begin (), inputScratch.end (), 0.0f);

		eventList.clear ();
		inputParameterChanges.clearQueue ();
		drainEvents ();
		drainParams ();

		processData.numSamples = frames;
		processContext.continousTimeSamples = continuousSamples;
		continuousSamples += frames;

		assignBuffers ();

		if (processor->process (processData) != kResultOk)
			return false;

		interleaveOutput (outputInterleaved, frames, channels);
		unassignBuffers ();
		return true;
	}

	bool processAudioWithInput (Sample32* inputInterleaved, Sample32* outputInterleaved, int32 frames,
	                            int32 inputChannelsCount, int32 outputChannelsCount)
	{
		if (suspendProcessing.load (std::memory_order_acquire))
			return false;
		if (!processing || !outputInterleaved || frames <= 0 || outputChannelsCount <= 0)
			return false;

		std::lock_guard<std::mutex> lock (processMutex);
		ensureScratchBuffers (frames);
		std::fill (outputScratch.begin (), outputScratch.end (), 0.0f);
		if (!inputScratch.empty ())
			std::fill (inputScratch.begin (), inputScratch.end (), 0.0f);

		if (inputInterleaved && inputChannelCount > 0 && inputChannelsCount > 0)
		{
			const auto copyChannels = std::min (inputChannelCount, inputChannelsCount);
			for (int32 frame = 0; frame < frames; ++frame)
			{
				for (int32 ch = 0; ch < inputChannelCount; ++ch)
				{
					float sample = 0.0f;
					if (ch < copyChannels)
					{
						sample = inputInterleaved[frame * inputChannelsCount + ch];
					}
					inputChannels[ch][frame] = sample;
				}
			}
		}

		eventList.clear ();
		inputParameterChanges.clearQueue ();
		drainEvents ();
		drainParams ();

		processData.numSamples = frames;
		processContext.continousTimeSamples = continuousSamples;
		continuousSamples += frames;

		assignBuffers ();

		if (processor->process (processData) != kResultOk)
			return false;

		interleaveOutput (outputInterleaved, frames, outputChannelsCount);
		unassignBuffers ();
		return true;
	}

	void noteOn (int32 note, int32 velocity, int32 channel)
	{
		if (suspendProcessing.load (std::memory_order_acquire))
			return;
		Event ev {};
		ev.type = Event::kNoteOnEvent;
		ev.noteOn.channel = static_cast<int16> (channel);
		ev.noteOn.pitch = static_cast<int16> (note);
		ev.noteOn.velocity = std::clamp (velocity / 127.0f, 0.0f, 1.0f);
		ev.noteOn.noteId = -1;
		ev.busIndex = 0;
		queueEvent (ev);
	}

	void noteOff (int32 note, int32 velocity, int32 channel)
	{
		if (suspendProcessing.load (std::memory_order_acquire))
			return;
		Event ev {};
		ev.type = Event::kNoteOffEvent;
		ev.noteOff.channel = static_cast<int16> (channel);
		ev.noteOff.pitch = static_cast<int16> (note);
		ev.noteOff.velocity = std::clamp (velocity / 127.0f, 0.0f, 1.0f);
		ev.noteOff.noteId = -1;
		ev.busIndex = 0;
		queueEvent (ev);
	}

	void allNotesOff (int32 channel)
	{
		if (suspendProcessing.load (std::memory_order_acquire))
			return;
		for (int32 note = 0; note < 128; ++note)
		{
			noteOff (note, 0, channel);
		}
	}

	void pitchBend (float normalized, int32 channel)
	{
		if (suspendProcessing.load (std::memory_order_acquire))
			return;
		if (!midiMapping)
			return;

		channel = std::clamp (channel, 0, 15);
		if (!pitchParamValid[channel])
		{
			ParamID paramId = kNoParamId;
			if (midiMapping->getMidiControllerAssignment (0, static_cast<int16> (channel),
			                                              kPitchBend, paramId) == kResultTrue)
			{
				pitchParamIds[channel] = paramId;
			}
			pitchParamValid[channel] = true;
		}

		auto paramId = pitchParamIds[channel];
		if (paramId == kNoParamId)
			return;

		auto value = static_cast<ParamValue> (std::clamp ((normalized + 1.0f) * 0.5f, 0.0f, 1.0f));
		queueParam (paramId, value);
	}

	bool getParameterInfo (int32 index, ParamID* id, char* nameUtf8, int32 nameCapacity)
	{
		if (!controller || !id || !nameUtf8 || nameCapacity <= 0)
			return false;

		ParameterInfo info {};
		if (controller->getParameterInfo (index, info) != kResultOk)
			return false;

		*id = info.id;
		auto name = StringConvert::convert (info.title, 128);
		if (name.empty ())
			return false;

		const auto copyLen = std::min<int32> (static_cast<int32> (name.size ()), nameCapacity - 1);
		if (copyLen > 0)
			memcpy (nameUtf8, name.data (), static_cast<size_t> (copyLen));
		nameUtf8[copyLen] = '\0';
		return true;
	}

	void setParameter (ParamID id, ParamValue value)
	{
		queueParam (id, std::clamp (value, 0.0, 1.0));
	}

	bool getEditorSize (int32* width, int32* height) const
	{
		if (!view || !width || !height)
			return false;
		ViewRect size {};
		if (view->getSize (&size) != kResultTrue)
			return false;
		*width = size.right - size.left;
		*height = size.bottom - size.top;
		return *width > 0 && *height > 0;
	}

	bool resizeEditor (int32 width, int32 height)
	{
		if (!view || width <= 0 || height <= 0)
			return false;
#if !defined(_WIN32)
		return false;
#else
		if (view->canResize () == kResultFalse)
			return false;

		ViewRect newSize {0, 0, width, height};
		if (view->checkSizeConstraint (&newSize) == kResultFalse)
			return false;
		if (view->onSize (&newSize) != kResultTrue)
			return false;

		if (frame)
		{
			if (auto hwnd = frame->hwnd ())
			{
				const int newWidth = newSize.right - newSize.left;
				const int newHeight = newSize.bottom - newSize.top;
				::SetWindowPos (static_cast<HWND> (hwnd), nullptr, 0, 0, newWidth, newHeight, SWP_NOZORDER | SWP_NOMOVE);
			}
		}
		return true;
#endif
	}

	int32 getOutputChannels () const { return outputChannelCount > 0 ? outputChannelCount : 2; }
	int32 getInputChannels () const { return inputChannelCount > 0 ? inputChannelCount : 0; }

	int32 getStateSize ()
	{
		std::vector<uint8> data;
		if (!getStateBlob (data))
			return 0;
		return static_cast<int32> (data.size ());
	}

	int32 copyStateToBuffer (uint8_t* buffer, int32 bufferSize)
	{
		if (!buffer || bufferSize <= 0)
			return 0;

		std::vector<uint8> data;
		if (!getStateBlob (data))
			return 0;
		if (static_cast<int32> (data.size ()) > bufferSize)
			return 0;

		memcpy (buffer, data.data (), data.size ());
		return static_cast<int32> (data.size ());
	}

	bool applyStateFromBuffer (const uint8_t* data, int32 size)
	{
		if (!data || size <= 0)
			return false;
		return setStateBlob (data, static_cast<size_t> (size));
	}

	bool getStateBlob (std::vector<uint8>& outData)
	{
		std::lock_guard<std::mutex> lock (processMutex);
		if (!component)
			return false;

		ResizableMemoryIBStream componentStream;
		tresult compResult = component->getState (&componentStream);
		if (compResult != kResultTrue)
			return false;

		std::vector<uint8> componentData = componentStream.take ();
		std::vector<uint8> controllerData;

		if (controller)
		{
			ResizableMemoryIBStream controllerStream;
			if (controller->getState (&controllerStream) == kResultTrue)
				controllerData = controllerStream.take ();
		}

		uint32 compSize = static_cast<uint32> (componentData.size ());
		uint32 ctrlSize = static_cast<uint32> (controllerData.size ());

		outData.clear ();
		outData.reserve (sizeof (uint32) * 2 + componentData.size () + controllerData.size ());

		auto appendBytes = [&outData] (const void* data, size_t size) {
			const uint8* bytes = static_cast<const uint8*> (data);
			outData.insert (outData.end (), bytes, bytes + size);
		};

		appendBytes (&compSize, sizeof (uint32));
		appendBytes (&ctrlSize, sizeof (uint32));
		if (!componentData.empty ())
			appendBytes (componentData.data (), componentData.size ());
		if (!controllerData.empty ())
			appendBytes (controllerData.data (), controllerData.size ());

		return true;
	}

	bool setStateBlob (const uint8* data, size_t size)
	{
		std::lock_guard<std::mutex> lock (processMutex);
		if (!component || !data || size < sizeof (uint32) * 2)
			return false;

		uint32 compSize = 0;
		uint32 ctrlSize = 0;
		memcpy (&compSize, data, sizeof (uint32));
		memcpy (&ctrlSize, data + sizeof (uint32), sizeof (uint32));

		size_t offset = sizeof (uint32) * 2;
		if (offset + compSize > size)
			return false;

		ResizableMemoryIBStream componentStream;
		if (compSize > 0)
		{
			componentStream.write (const_cast<uint8*> (data + offset), static_cast<int32> (compSize), nullptr);
			componentStream.rewind ();
			component->setState (&componentStream);
		}

		offset += compSize;
		if (offset + ctrlSize > size)
			return false;

		if (controller && ctrlSize > 0)
		{
			ResizableMemoryIBStream controllerStream;
			controllerStream.write (const_cast<uint8*> (data + offset), static_cast<int32> (ctrlSize), nullptr);
			controllerStream.rewind ();
			controller->setState (&controllerStream);
		}

		return true;
	}

private:
	void activateBusses ()
	{
		if (!component)
			return;

		const auto audioOutCount = component->getBusCount (kAudio, kOutput);
		for (int32 i = 0; i < audioOutCount; ++i)
			component->activateBus (kAudio, kOutput, i, true);

		const auto audioInCount = component->getBusCount (kAudio, kInput);
		for (int32 i = 0; i < audioInCount; ++i)
			component->activateBus (kAudio, kInput, i, true);

		const auto eventInCount = component->getBusCount (kEvent, kInput);
		for (int32 i = 0; i < eventInCount; ++i)
			component->activateBus (kEvent, kInput, i, true);
	}

	void cacheBusChannels ()
	{
		outputChannelCount = 0;
		inputChannelCount = 0;

		if (!component)
			return;

		const auto outBusses = component->getBusCount (kAudio, kOutput);
		for (int32 i = 0; i < outBusses; ++i)
		{
			BusInfo info {};
			if (component->getBusInfo (kAudio, kOutput, i, info) == kResultOk)
				outputChannelCount += info.channelCount;
		}

		const auto inBusses = component->getBusCount (kAudio, kInput);
		for (int32 i = 0; i < inBusses; ++i)
		{
			BusInfo info {};
			if (component->getBusInfo (kAudio, kInput, i, info) == kResultOk)
				inputChannelCount += info.channelCount;
		}
	}

	void ensureScratchBuffers (int32 frames)
	{
		const auto outChannels = getOutputChannels ();
		const auto outSize = static_cast<size_t> (outChannels) * frames;
		if (outputScratch.size () != outSize)
			outputScratch.assign (outSize, 0.0f);

		outputChannels.resize (outChannels);
		for (int32 ch = 0; ch < outChannels; ++ch)
			outputChannels[ch] = outputScratch.data () + (static_cast<size_t> (ch) * frames);

		const auto inChannels = inputChannelCount;
		const auto inSize = static_cast<size_t> (inChannels) * frames;
		if (inChannels > 0)
		{
			if (inputScratch.size () != inSize)
				inputScratch.assign (inSize, 0.0f);

			inputChannels.resize (inChannels);
			for (int32 ch = 0; ch < inChannels; ++ch)
				inputChannels[ch] = inputScratch.data () + (static_cast<size_t> (ch) * frames);
		}
		else
		{
			inputScratch.clear ();
			inputChannels.clear ();
		}
	}

	void assignBuffers ()
	{
		int32 channelIndex = 0;
		for (int32 bus = 0; bus < processData.numOutputs; ++bus)
		{
			const auto channelCount = processData.outputs[bus].numChannels;
			for (int32 ch = 0; ch < channelCount; ++ch)
			{
				Sample32* buffer = nullptr;
				if (channelIndex < static_cast<int32> (outputChannels.size ()))
					buffer = outputChannels[channelIndex++];
				processData.setChannelBuffer (kOutput, bus, ch, buffer);
			}
		}

		channelIndex = 0;
		for (int32 bus = 0; bus < processData.numInputs; ++bus)
		{
			const auto channelCount = processData.inputs[bus].numChannels;
			for (int32 ch = 0; ch < channelCount; ++ch)
			{
				Sample32* buffer = nullptr;
				if (channelIndex < static_cast<int32> (inputChannels.size ()))
					buffer = inputChannels[channelIndex++];
				processData.setChannelBuffer (kInput, bus, ch, buffer);
			}
		}
	}

	void unassignBuffers ()
	{
		for (int32 bus = 0; bus < processData.numOutputs; ++bus)
		{
			const auto channelCount = processData.outputs[bus].numChannels;
			for (int32 ch = 0; ch < channelCount; ++ch)
				processData.setChannelBuffer (kOutput, bus, ch, nullptr);
		}

		for (int32 bus = 0; bus < processData.numInputs; ++bus)
		{
			const auto channelCount = processData.inputs[bus].numChannels;
			for (int32 ch = 0; ch < channelCount; ++ch)
				processData.setChannelBuffer (kInput, bus, ch, nullptr);
		}
	}

	void interleaveOutput (Sample32* outputInterleaved, int32 frames, int32 channels)
	{
		const auto outChannels = getOutputChannels ();
		const auto copyChannels = std::min (outChannels, channels);

		for (int32 frame = 0; frame < frames; ++frame)
		{
			for (int32 ch = 0; ch < copyChannels; ++ch)
			{
				outputInterleaved[frame * channels + ch] =
				    outputChannels[ch] ? outputChannels[ch][frame] : 0.0f;
			}
			for (int32 ch = copyChannels; ch < channels; ++ch)
				outputInterleaved[frame * channels + ch] = 0.0f;
		}
	}

	void queueEvent (const Event& ev)
	{
		std::lock_guard<std::mutex> lock (eventMutex);
		pendingEvents.push_back (ev);
	}

	void queueParam (ParamID id, ParamValue value)
	{
		std::lock_guard<std::mutex> lock (eventMutex);
		pendingParams.push_back ({id, value});
	}

	void drainEvents ()
	{
		std::vector<Event> events;
		{
			std::lock_guard<std::mutex> lock (eventMutex);
			events.swap (pendingEvents);
		}

		for (auto& ev : events)
		{
			ev.sampleOffset = 0;
			if (eventList.addEvent (ev) != kResultOk)
				break;
		}
	}

	void drainParams ()
	{
		std::vector<PendingParam> params;
		{
			std::lock_guard<std::mutex> lock (eventMutex);
			params.swap (pendingParams);
		}

		for (auto& param : params)
		{
			int32 index = 0;
			IParamValueQueue* queue = inputParameterChanges.addParameterData (param.id, index);
			if (!queue)
				continue;
			queue->addPoint (0, param.value, index);
		}
	}
};
} // namespace

extern "C"
{
__declspec (dllexport) void* __cdecl Vst3Host_Create (const char* path)
{
	if (!path)
		return nullptr;

	auto instance = std::make_unique<Vst3HostInstance> ();
	if (!instance->initialize (path))
		return nullptr;

	return instance.release ();
}

__declspec (dllexport) bool __cdecl Vst3Host_OpenEditor (void* handle, void* hwnd)
{
	if (!handle || !hwnd)
		return false;

	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	return instance->openEditor (hwnd);
}

__declspec (dllexport) void __cdecl Vst3Host_Close (void* handle)
{
	if (!handle)
		return;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	instance->closeEditor ();
	delete instance;
}

__declspec (dllexport) bool __cdecl Vst3Host_SetupAudio (void* handle, int sampleRate, int blockSize)
{
	if (!handle)
		return false;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	return instance->setupAudio (sampleRate, blockSize);
}

__declspec (dllexport) int __cdecl Vst3Host_GetOutputChannels (void* handle)
{
	if (!handle)
		return 0;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	return instance->getOutputChannels ();
}

__declspec (dllexport) int __cdecl Vst3Host_GetInputChannels (void* handle)
{
	if (!handle)
		return 0;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	return instance->getInputChannels ();
}

__declspec (dllexport) bool __cdecl Vst3Host_Process (void* handle, float* outputInterleaved,
                                                     int frames, int channels)
{
	if (!handle)
		return false;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	return instance->processAudio (outputInterleaved, frames, channels);
}

__declspec (dllexport) bool __cdecl Vst3Host_ProcessWithInput (void* handle, float* inputInterleaved,
                                                              float* outputInterleaved, int frames,
                                                              int inputChannels, int outputChannels)
{
	if (!handle)
		return false;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	return instance->processAudioWithInput (inputInterleaved, outputInterleaved, frames, inputChannels,
	                                        outputChannels);
}

__declspec (dllexport) void __cdecl Vst3Host_SendNoteOn (void* handle, int note, int velocity,
                                                       int channel)
{
	if (!handle)
		return;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	instance->noteOn (note, velocity, channel);
}

__declspec (dllexport) void __cdecl Vst3Host_SendNoteOff (void* handle, int note, int velocity,
                                                        int channel)
{
	if (!handle)
		return;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	instance->noteOff (note, velocity, channel);
}

__declspec (dllexport) void __cdecl Vst3Host_AllNotesOff (void* handle, int channel)
{
	if (!handle)
		return;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	instance->allNotesOff (channel);
}

__declspec (dllexport) void __cdecl Vst3Host_SendPitchBend (void* handle, float normalized,
                                                          int channel)
{
	if (!handle)
		return;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	instance->pitchBend (normalized, channel);
}

__declspec (dllexport) int __cdecl Vst3Host_GetParameterCount (void* handle)
{
	if (!handle)
		return 0;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	if (!instance->controller)
		return 0;
	return instance->controller->getParameterCount ();
}

__declspec (dllexport) bool __cdecl Vst3Host_GetParameterInfo (void* handle, int index, int* id,
                                                              char* nameUtf8, int nameCapacity)
{
	if (!handle)
		return false;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	ParamID paramId = kNoParamId;
	if (!instance->getParameterInfo (index, &paramId, nameUtf8, nameCapacity))
		return false;
	if (id)
		*id = paramId;
	return true;
}

__declspec (dllexport) void __cdecl Vst3Host_SetParameter (void* handle, int id, double normalized)
{
	if (!handle)
		return;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	instance->setParameter (static_cast<ParamID> (id), normalized);
}

__declspec (dllexport) bool __cdecl Vst3Host_GetEditorSize (void* handle, int* width, int* height)
{
	if (!handle)
		return false;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	return instance->getEditorSize (width, height);
}

__declspec (dllexport) bool __cdecl Vst3Host_ResizeEditor (void* handle, int width, int height)
{
	if (!handle)
		return false;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
	return instance->resizeEditor (width, height);
}

__declspec (dllexport) int __cdecl Vst3Host_GetStateSize (void* handle)
{
	if (!handle)
		return 0;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
#if defined(_WIN32)
	__try
	{
#endif
	return static_cast<int> (instance->getStateSize ());
#if defined(_WIN32)
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return 0;
	}
#endif
}

__declspec (dllexport) int __cdecl Vst3Host_GetState (void* handle, uint8_t* buffer, int bufferSize)
{
	if (!handle || !buffer || bufferSize <= 0)
		return 0;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
#if defined(_WIN32)
	__try
	{
#endif
	return static_cast<int> (instance->copyStateToBuffer (buffer, bufferSize));
#if defined(_WIN32)
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return 0;
	}
#endif
}

__declspec (dllexport) bool __cdecl Vst3Host_SetState (void* handle, const uint8_t* data, int size)
{
	if (!handle || !data || size <= 0)
		return false;
	auto instance = reinterpret_cast<Vst3HostInstance*> (handle);
#if defined(_WIN32)
	__try
	{
#endif
	return instance->applyStateFromBuffer (data, size);
#if defined(_WIN32)
	}
	__except (EXCEPTION_EXECUTE_HANDLER)
	{
		return false;
	}
#endif
}
}
