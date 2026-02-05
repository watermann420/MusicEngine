
#include "VstBridge.h"

#include <algorithm>
#include <atomic>
#include <cctype>
#include <cstring>
#include <mutex>
#include <string>
#include <vector>

#include "public.sdk/source/vst/hosting/eventlist.h"
#include "public.sdk/source/vst/hosting/hostclasses.h"
#include "public.sdk/source/vst/hosting/module.h"
#include "public.sdk/source/vst/hosting/parameterchanges.h"
#include "public.sdk/source/vst/hosting/plugprovider.h"
#include "public.sdk/source/vst/hosting/processdata.h"
#include "public.sdk/source/vst/utility/stringconvert.h"
#include "pluginterfaces/base/funknown.h"
#include "pluginterfaces/base/ibstream.h"
#include "pluginterfaces/gui/iplugview.h"
#include "pluginterfaces/vst/ivstaudioprocessor.h"
#include "pluginterfaces/vst/ivsteditcontroller.h"
#include "pluginterfaces/vst/ivstevents.h"
#include "pluginterfaces/vst/ivstmidicontrollers.h"
#include "pluginterfaces/vst/vsttypes.h"

using namespace Steinberg;
using namespace Steinberg::Vst;

namespace
{
    struct MidiEvent
    {
        int status = 0;
        int data1 = 0;
        int data2 = 0;
        int deltaFrames = 0;
    };

    class MemoryStream : public IBStream, public ISizeableStream
    {
    public:
        MemoryStream() { __funknownRefCount = 1; }

        tresult PLUGIN_API queryInterface(const TUID _iid, void** obj) override
        {
            if (FUnknownPrivate::iidEqual(_iid, IBStream::iid))
            {
                addRef();
                *obj = static_cast<IBStream*>(this);
                return kResultOk;
            }
            if (FUnknownPrivate::iidEqual(_iid, ISizeableStream::iid))
            {
                addRef();
                *obj = static_cast<ISizeableStream*>(this);
                return kResultOk;
            }
            if (FUnknownPrivate::iidEqual(_iid, FUnknown::iid))
            {
                addRef();
                *obj = static_cast<IBStream*>(this);
                return kResultOk;
            }
            *obj = nullptr;
            return kNoInterface;
        }

        uint32 PLUGIN_API addRef() override
        {
            return FUnknownPrivate::atomicAdd(__funknownRefCount, 1);
        }

        uint32 PLUGIN_API release() override
        {
            auto count = FUnknownPrivate::atomicAdd(__funknownRefCount, -1);
            if (count == 0)
            {
                delete this;
            }
            return count;
        }

        tresult PLUGIN_API read(void* buffer, int32 numBytes, int32* numBytesRead) override
        {
            if (!buffer || numBytes <= 0)
            {
                if (numBytesRead) *numBytesRead = 0;
                return kInvalidArgument;
            }

            int64 available = static_cast<int64>(data.size()) - position;
            int32 toRead = static_cast<int32>(std::min<int64>(available, numBytes));
            if (toRead > 0)
            {
                std::memcpy(buffer, data.data() + position, static_cast<size_t>(toRead));
                position += toRead;
            }
            if (numBytesRead) *numBytesRead = toRead;
            return kResultOk;
        }

        tresult PLUGIN_API write(void* buffer, int32 numBytes, int32* numBytesWritten) override
        {
            if (!buffer || numBytes <= 0)
            {
                if (numBytesWritten) *numBytesWritten = 0;
                return kInvalidArgument;
            }

            if (position + numBytes > static_cast<int64>(data.size()))
            {
                data.resize(static_cast<size_t>(position + numBytes));
            }

            std::memcpy(data.data() + position, buffer, static_cast<size_t>(numBytes));
            position += numBytes;
            if (numBytesWritten) *numBytesWritten = numBytes;
            return kResultOk;
        }

        tresult PLUGIN_API seek(int64 pos, int32 mode, int64* result) override
        {
            int64 newPos = position;
            switch (mode)
            {
                case IBStream::kIBSeekSet:
                    newPos = pos;
                    break;
                case IBStream::kIBSeekCur:
                    newPos = position + pos;
                    break;
                case IBStream::kIBSeekEnd:
                    newPos = static_cast<int64>(data.size()) + pos;
                    break;
                default:
                    return kInvalidArgument;
            }

            if (newPos < 0)
            {
                return kInvalidArgument;
            }

            position = newPos;
            if (result) *result = position;
            return kResultOk;
        }

        tresult PLUGIN_API tell(int64* pos) override
        {
            if (!pos)
            {
                return kInvalidArgument;
            }
            *pos = position;
            return kResultOk;
        }

        tresult PLUGIN_API getStreamSize(int64& size) override
        {
            size = static_cast<int64>(data.size());
            return kResultOk;
        }

        tresult PLUGIN_API setStreamSize(int64 size) override
        {
            if (size < 0)
            {
                return kInvalidArgument;
            }
            data.resize(static_cast<size_t>(size));
            if (position > size)
            {
                position = size;
            }
            return kResultOk;
        }

        const std::vector<unsigned char>& getData() const { return data; }
        void setData(const unsigned char* buffer, int32 size)
        {
            if (!buffer || size <= 0)
            {
                data.clear();
                position = 0;
                return;
            }
            data.assign(buffer, buffer + size);
            position = 0;
        }

    private:
        int32 __funknownRefCount {1};
        std::vector<unsigned char> data;
        int64 position {0};
    };

    class SimplePlugFrame : public IPlugFrame
    {
    public:
        SimplePlugFrame() { __funknownRefCount = 1; }

        tresult PLUGIN_API resizeView(IPlugView* /*view*/, ViewRect* newSize) override
        {
            if (newSize)
            {
                lastSize = *newSize;
            }
            return kResultOk;
        }

        tresult PLUGIN_API queryInterface(const TUID _iid, void** obj) override
        {
            QUERY_INTERFACE(_iid, obj, IPlugFrame::iid, IPlugFrame)
            QUERY_INTERFACE(_iid, obj, FUnknown::iid, FUnknown)
            *obj = nullptr;
            return kNoInterface;
        }

        uint32 PLUGIN_API addRef() override
        {
            return FUnknownPrivate::atomicAdd(__funknownRefCount, 1);
        }

        uint32 PLUGIN_API release() override
        {
            auto count = FUnknownPrivate::atomicAdd(__funknownRefCount, -1);
            if (count == 0)
            {
                delete this;
            }
            return count;
        }

        ViewRect lastSize {};

    private:
        int32 __funknownRefCount {1};
    };
    struct NativePlugin
    {
        std::string path;
        std::string name;
        std::string vendor;
        std::string product;
        std::string version;
        uint32_t uniqueId = 0;
        int numInputs = 0;
        int numOutputs = 0;
        bool isSynth = false;
        int latency = 0;
        int tailSize = 0;
        int parameterCount = 0;
        bool hasEditor = false;
        bool isEditorOpen = false;
        int sampleRate = 44100;
        int blockSize = 512;
        int programCount = 0;
        int currentProgram = 0;

        VST3::Hosting::Module::Ptr module;
        VST3::Hosting::PluginFactory factory {nullptr};
        VST3::Hosting::ClassInfo classInfo;
        std::unique_ptr<PlugProvider> plugProvider;
        IPtr<IComponent> component;
        IPtr<IEditController> controller;
        IPtr<IAudioProcessor> processor;

        HostProcessData processData;
        EventList inputEvents {128};
        EventList outputEvents {128};
        ParameterChanges inputParameterChanges {128};
        ProcessContext processContext {};

        std::vector<ParamID> parameterIds;
        std::vector<MidiEvent> pendingMidi;
        std::mutex midiMutex;

        IPtr<IPlugView> plugView;
        IPtr<IPlugFrame> plugFrame;
    };

    struct NativeHost
    {
        int sampleRate = 44100;
        int blockSize = 512;
        std::vector<NativePlugin*> plugins;
        std::string lastError;
        std::mutex mutex;
        IPtr<HostApplication> hostApplication;
    };

    int write_string(const std::string& value, char* buffer, int bufferSize)
    {
        if (!buffer || bufferSize <= 0)
        {
            return 0;
        }

        int length = static_cast<int>(value.size());
        int copyLength = std::min(length, bufferSize - 1);
        if (copyLength > 0)
        {
            std::memcpy(buffer, value.data(), copyLength);
        }
        buffer[copyLength] = '\0';
        return copyLength;
    }

    NativePlugin* as_plugin(void* plugin)
    {
        return static_cast<NativePlugin*>(plugin);
    }

    NativeHost* as_host(void* host)
    {
        return static_cast<NativeHost*>(host);
    }

    bool is_vst3_path(const std::string& path)
    {
        if (path.size() < 5)
        {
            return false;
        }
        auto lower = path;
        std::transform(lower.begin(), lower.end(), lower.begin(), [](unsigned char c) { return static_cast<char>(std::tolower(c)); });
        return lower.rfind(".vst3") == lower.size() - 5;
    }

    bool is_instrument(const VST3::Hosting::ClassInfo& info)
    {
        for (const auto& sub : info.subCategories())
        {
            if (sub == "Instrument")
            {
                return true;
            }
        }
        return false;
    }

    SpeakerArrangement arrangement_for_channels(int32 channels)
    {
        switch (channels)
        {
            case 1: return SpeakerArr::kMono;
            case 2: return SpeakerArr::kStereo;
            case 6: return SpeakerArr::k51;
            case 8: return SpeakerArr::k71Cine;
            default: return SpeakerArr::kStereo;
        }
    }

    void setup_bus_arrangements(NativePlugin& plugin)
    {
        if (!plugin.processor || !plugin.component)
        {
            return;
        }

        int32 inputBuses = plugin.component->getBusCount(kAudio, kInput);
        int32 outputBuses = plugin.component->getBusCount(kAudio, kOutput);

        std::vector<SpeakerArrangement> inputs(static_cast<size_t>(inputBuses));
        std::vector<SpeakerArrangement> outputs(static_cast<size_t>(outputBuses));

        for (int32 i = 0; i < inputBuses; ++i)
        {
            BusInfo busInfo {};
            if (plugin.component->getBusInfo(kAudio, kInput, i, busInfo) == kResultTrue)
            {
                inputs[static_cast<size_t>(i)] = arrangement_for_channels(busInfo.channelCount);
            }
            else
            {
                inputs[static_cast<size_t>(i)] = SpeakerArr::kStereo;
            }
        }

        for (int32 i = 0; i < outputBuses; ++i)
        {
            BusInfo busInfo {};
            if (plugin.component->getBusInfo(kAudio, kOutput, i, busInfo) == kResultTrue)
            {
                outputs[static_cast<size_t>(i)] = arrangement_for_channels(busInfo.channelCount);
            }
            else
            {
                outputs[static_cast<size_t>(i)] = SpeakerArr::kStereo;
            }
        }

        plugin.processor->setBusArrangements(inputs.data(), inputBuses, outputs.data(), outputBuses);
    }
    void activate_buses(NativePlugin& plugin)
    {
        if (!plugin.component)
        {
            return;
        }

        int32 inputBuses = plugin.component->getBusCount(kAudio, kInput);
        int32 outputBuses = plugin.component->getBusCount(kAudio, kOutput);

        for (int32 i = 0; i < inputBuses; ++i)
        {
            plugin.component->activateBus(kAudio, kInput, i, true);
        }
        for (int32 i = 0; i < outputBuses; ++i)
        {
            plugin.component->activateBus(kAudio, kOutput, i, true);
        }
    }

    void update_processing_setup(NativePlugin& plugin)
    {
        if (!plugin.processor)
        {
            return;
        }

        ProcessSetup setup {};
        setup.sampleRate = plugin.sampleRate;
        setup.maxSamplesPerBlock = plugin.blockSize;
        setup.processMode = kRealtime;
        setup.symbolicSampleSize = kSample32;

        plugin.processor->setupProcessing(setup);
        plugin.processor->setProcessing(true);
    }

    void refresh_plugin_info(NativePlugin& plugin)
    {
        plugin.parameterCount = 0;
        plugin.parameterIds.clear();

        if (plugin.controller)
        {
            int32 count = plugin.controller->getParameterCount();
            plugin.parameterCount = count;
            plugin.parameterIds.reserve(static_cast<size_t>(count));
            for (int32 i = 0; i < count; ++i)
            {
                ParameterInfo info {};
                if (plugin.controller->getParameterInfo(i, info) == kResultTrue)
                {
                    plugin.parameterIds.push_back(info.id);
                }
                else
                {
                    plugin.parameterIds.push_back(static_cast<ParamID>(i));
                }
            }
        }

        if (plugin.processor)
        {
            plugin.latency = plugin.processor->getLatencySamples();
            plugin.tailSize = plugin.processor->getTailSamples();
        }

        if (plugin.component)
        {
            BusInfo busInfo {};
            if (plugin.component->getBusInfo(kAudio, kInput, 0, busInfo) == kResultTrue)
            {
                plugin.numInputs = busInfo.channelCount;
            }
            if (plugin.component->getBusInfo(kAudio, kOutput, 0, busInfo) == kResultTrue)
            {
                plugin.numOutputs = busInfo.channelCount;
            }
        }

        if (plugin.controller)
        {
            plugin.hasEditor = plugin.controller->createView(ViewType::kEditor) != nullptr;
        }
    }

    void fill_event_list(NativePlugin& plugin, int sampleCount)
    {
        std::lock_guard<std::mutex> lock(plugin.midiMutex);
        plugin.inputEvents.clear();

        for (const auto& midi : plugin.pendingMidi)
        {
            Event event {};
            event.busIndex = 0;
            event.sampleOffset = std::clamp(midi.deltaFrames, 0, sampleCount - 1);
            event.ppqPosition = 0.0;
            event.flags = 0;

            int statusType = midi.status & 0xF0;
            int channel = midi.status & 0x0F;

            if (statusType == 0x90 && midi.data2 > 0)
            {
                event.type = Event::kNoteOnEvent;
                event.noteOn.channel = static_cast<int16>(channel);
                event.noteOn.pitch = static_cast<int16>(midi.data1 & 0x7F);
                event.noteOn.velocity = static_cast<float>(midi.data2) / 127.0f;
                event.noteOn.tuning = 0.0f;
                event.noteOn.length = 0;
                event.noteOn.noteId = -1;
            }
            else if (statusType == 0x80 || (statusType == 0x90 && midi.data2 == 0))
            {
                event.type = Event::kNoteOffEvent;
                event.noteOff.channel = static_cast<int16>(channel);
                event.noteOff.pitch = static_cast<int16>(midi.data1 & 0x7F);
                event.noteOff.velocity = static_cast<float>(midi.data2) / 127.0f;
                event.noteOff.tuning = 0.0f;
                event.noteOff.noteId = -1;
            }
            else if (statusType == 0xB0)
            {
                event.type = Event::kLegacyMIDICCOutEvent;
                event.midiCCOut.channel = static_cast<int8>(channel);
                event.midiCCOut.controlNumber = static_cast<uint8>(midi.data1 & 0x7F);
                event.midiCCOut.value = static_cast<int8>(midi.data2 & 0x7F);
                event.midiCCOut.value2 = 0;
            }
            else if (statusType == 0xE0)
            {
                event.type = Event::kLegacyMIDICCOutEvent;
                event.midiCCOut.channel = static_cast<int8>(channel);
                event.midiCCOut.controlNumber = kPitchBend;
                event.midiCCOut.value = static_cast<int8>(midi.data1 & 0x7F);
                event.midiCCOut.value2 = static_cast<int8>(midi.data2 & 0x7F);
            }
            else if (statusType == 0xC0)
            {
                event.type = Event::kLegacyMIDICCOutEvent;
                event.midiCCOut.channel = static_cast<int8>(channel);
                event.midiCCOut.controlNumber = kCtrlProgramChange;
                event.midiCCOut.value = static_cast<int8>(midi.data1 & 0x7F);
                event.midiCCOut.value2 = 0;
            }
            else
            {
                continue;
            }

            plugin.inputEvents.addEvent(event);
        }

        plugin.pendingMidi.clear();
    }

    void setup_process_buffers(NativePlugin& plugin, float** inputs, float** outputs, int numInputs, int numOutputs, int sampleCount)
    {
        if (!plugin.component)
        {
            return;
        }

        plugin.processData.prepare(*plugin.component, 0, kSample32);
        plugin.processData.numSamples = sampleCount;
        plugin.processData.processMode = kRealtime;
        plugin.processData.symbolicSampleSize = kSample32;
        plugin.processData.inputEvents = plugin.inputEvents.getEventCount() > 0 ? &plugin.inputEvents : nullptr;
        plugin.processData.outputEvents = &plugin.outputEvents;
        plugin.processData.inputParameterChanges = nullptr;
        plugin.processData.outputParameterChanges = nullptr;
        plugin.processData.processContext = &plugin.processContext;

        for (int32 i = 0; i < plugin.processData.numInputs; ++i)
        {
            for (int32 ch = 0; ch < plugin.processData.inputs[i].numChannels; ++ch)
            {
                float* buffer = nullptr;
                if (i == 0 && ch < numInputs && inputs)
                {
                    buffer = inputs[ch];
                }
                plugin.processData.setChannelBuffer(kInput, i, ch, buffer);
            }
        }

        for (int32 i = 0; i < plugin.processData.numOutputs; ++i)
        {
            for (int32 ch = 0; ch < plugin.processData.outputs[i].numChannels; ++ch)
            {
                float* buffer = nullptr;
                if (i == 0 && ch < numOutputs && outputs)
                {
                    buffer = outputs[ch];
                }
                plugin.processData.setChannelBuffer(kOutput, i, ch, buffer);
            }
        }
    }

    std::string get_string128(const TChar* text)
    {
        if (!text)
        {
            return {};
        }
        return StringConvert::convert(text);
    }

    ParamID get_param_id(const NativePlugin& plugin, int index)
    {
        if (index < 0 || index >= static_cast<int>(plugin.parameterIds.size()))
        {
            return static_cast<ParamID>(index);
        }
        return plugin.parameterIds[static_cast<size_t>(index)];
    }
}
extern "C"
{
    int me_vst_is_available()
    {
        return 1;
    }

    int me_vst_get_version(char* buffer, int bufferSize)
    {
        return write_string("VST3", buffer, bufferSize);
    }

    int me_vst_has_vst2()
    {
        return 0;
    }

    int me_vst_has_vst3()
    {
        return 1;
    }

    void* me_vst_host_create(int sampleRate, int blockSize)
    {
        auto* host = new NativeHost();
        host->sampleRate = sampleRate;
        host->blockSize = blockSize;
        host->hostApplication = owned(new HostApplication());
        PluginContextFactory::instance().setPluginContext(host->hostApplication);
        return host;
    }

    void me_vst_host_destroy(void* host)
    {
        auto* nativeHost = as_host(host);
        if (!nativeHost)
        {
            return;
        }

        me_vst_host_unload_all(nativeHost);
        nativeHost->hostApplication = nullptr;
        delete nativeHost;
    }

    void me_vst_host_set_sample_rate(void* host, int sampleRate)
    {
        auto* nativeHost = as_host(host);
        if (!nativeHost)
        {
            return;
        }

        nativeHost->sampleRate = sampleRate;
        std::lock_guard<std::mutex> lock(nativeHost->mutex);
        for (auto* plugin : nativeHost->plugins)
        {
            plugin->sampleRate = sampleRate;
            update_processing_setup(*plugin);
        }
    }

    void me_vst_host_set_block_size(void* host, int blockSize)
    {
        auto* nativeHost = as_host(host);
        if (!nativeHost)
        {
            return;
        }

        nativeHost->blockSize = blockSize;
        std::lock_guard<std::mutex> lock(nativeHost->mutex);
        for (auto* plugin : nativeHost->plugins)
        {
            plugin->blockSize = blockSize;
            update_processing_setup(*plugin);
        }
    }

    void* me_vst_host_load_plugin(void* host, const char* path)
    {
        auto* nativeHost = as_host(host);
        if (!nativeHost || !path)
        {
            return nullptr;
        }

        nativeHost->lastError.clear();
        std::string pluginPath = path;
        if (!is_vst3_path(pluginPath))
        {
            nativeHost->lastError = "VST2 is not supported. Use .vst3 plugins.";
            return nullptr;
        }

        std::string error;
        auto module = VST3::Hosting::Module::create(pluginPath, error);
        if (!module)
        {
            nativeHost->lastError = error;
            return nullptr;
        }

        auto factory = module->getFactory();
        factory.setHostContext(nativeHost->hostApplication);

        VST3::Hosting::ClassInfo selectedClass;
        bool found = false;
        for (auto& classInfo : factory.classInfos())
        {
            if (classInfo.category() == kVstAudioEffectClass)
            {
                selectedClass = classInfo;
                found = true;
                break;
            }
        }

        if (!found)
        {
            nativeHost->lastError = "No VST3 AudioEffect class found in module.";
            return nullptr;
        }

        auto* plugin = new NativePlugin();
        plugin->path = pluginPath;
        plugin->name = selectedClass.name();
        plugin->vendor = selectedClass.vendor();
        plugin->product = selectedClass.name();
        plugin->version = selectedClass.version();
        plugin->uniqueId = static_cast<uint32_t>(std::hash<std::string>{}(pluginPath));
        plugin->isSynth = is_instrument(selectedClass);
        plugin->sampleRate = nativeHost->sampleRate;
        plugin->blockSize = nativeHost->blockSize;
        plugin->module = module;
        plugin->factory = factory;
        plugin->classInfo = selectedClass;

        plugin->plugProvider = std::make_unique<PlugProvider>(factory, selectedClass, true);
        if (!plugin->plugProvider->initialize())
        {
            nativeHost->lastError = "Failed to initialize VST3 plug-in.";
            delete plugin;
            return nullptr;
        }

        plugin->component = plugin->plugProvider->getComponentPtr();
        plugin->controller = plugin->plugProvider->getControllerPtr();

        if (plugin->component)
        {
            IAudioProcessor* processorPtr = nullptr;
            plugin->component->queryInterface(IAudioProcessor::iid, reinterpret_cast<void**>(&processorPtr));
            if (processorPtr)
            {
                plugin->processor = owned(processorPtr);
            }
        }

        activate_buses(*plugin);
        setup_bus_arrangements(*plugin);
        update_processing_setup(*plugin);

        if (plugin->component)
        {
            plugin->component->setActive(true);
        }

        refresh_plugin_info(*plugin);

        std::lock_guard<std::mutex> lock(nativeHost->mutex);
        nativeHost->plugins.push_back(plugin);
        return plugin;
    }

    void me_vst_host_unload_plugin(void* host, void* plugin)
    {
        auto* nativeHost = as_host(host);
        auto* nativePlugin = as_plugin(plugin);
        if (!nativeHost || !nativePlugin)
        {
            return;
        }

        if (nativePlugin->processor)
        {
            nativePlugin->processor->setProcessing(false);
        }

        if (nativePlugin->component)
        {
            nativePlugin->component->setActive(false);
        }

        if (nativePlugin->plugView)
        {
            nativePlugin->plugView->removed();
            nativePlugin->plugView->setFrame(nullptr);
            nativePlugin->plugView = nullptr;
        }

        if (nativePlugin->plugProvider && nativePlugin->component)
        {
            nativePlugin->plugProvider->releasePlugIn(nativePlugin->component.get(),
                                                      nativePlugin->controller.get());
        }

        std::lock_guard<std::mutex> lock(nativeHost->mutex);
        auto& plugins = nativeHost->plugins;
        plugins.erase(std::remove(plugins.begin(), plugins.end(), nativePlugin), plugins.end());
        delete nativePlugin;
    }

    void me_vst_host_unload_all(void* host)
    {
        auto* nativeHost = as_host(host);
        if (!nativeHost)
        {
            return;
        }

        std::lock_guard<std::mutex> lock(nativeHost->mutex);
        for (auto* plugin : nativeHost->plugins)
        {
            if (plugin->processor)
            {
                plugin->processor->setProcessing(false);
            }
            if (plugin->component)
            {
                plugin->component->setActive(false);
            }
            delete plugin;
        }
        nativeHost->plugins.clear();
    }

    int me_vst_host_get_last_error(void* host, char* buffer, int bufferSize)
    {
        auto* nativeHost = as_host(host);
        if (!nativeHost)
        {
            return 0;
        }

        return write_string(nativeHost->lastError, buffer, bufferSize);
    }
    int me_vst_plugin_get_name(void* plugin, char* buffer, int bufferSize)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin)
        {
            return 0;
        }

        return write_string(nativePlugin->name, buffer, bufferSize);
    }

    int me_vst_plugin_get_vendor(void* plugin, char* buffer, int bufferSize)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin)
        {
            return 0;
        }

        return write_string(nativePlugin->vendor, buffer, bufferSize);
    }

    int me_vst_plugin_get_product(void* plugin, char* buffer, int bufferSize)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin)
        {
            return 0;
        }

        return write_string(nativePlugin->product, buffer, bufferSize);
    }

    int me_vst_plugin_get_version(void* plugin, char* buffer, int bufferSize)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin)
        {
            return 0;
        }

        return write_string(nativePlugin->version, buffer, bufferSize);
    }

    int me_vst_plugin_get_type(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin)
        {
            return 0;
        }

        return 1;
    }

    uint32_t me_vst_plugin_get_unique_id(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        return nativePlugin ? nativePlugin->uniqueId : 0;
    }

    int me_vst_plugin_get_num_inputs(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        return nativePlugin ? nativePlugin->numInputs : 0;
    }

    int me_vst_plugin_get_num_outputs(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        return nativePlugin ? nativePlugin->numOutputs : 0;
    }

    int me_vst_plugin_is_synth(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        return nativePlugin && nativePlugin->isSynth ? 1 : 0;
    }

    int me_vst_plugin_get_latency(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        return nativePlugin ? nativePlugin->latency : 0;
    }

    int me_vst_plugin_get_tail_size(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        return nativePlugin ? nativePlugin->tailSize : 0;
    }

    int me_vst_plugin_get_parameter_count(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        return nativePlugin ? nativePlugin->parameterCount : 0;
    }

    int me_vst_plugin_is_valid(void* plugin)
    {
        return plugin != nullptr ? 1 : 0;
    }

    int me_vst_plugin_has_editor(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        return nativePlugin && nativePlugin->hasEditor ? 1 : 0;
    }

    int me_vst_plugin_is_editor_open(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        return nativePlugin && nativePlugin->isEditorOpen ? 1 : 0;
    }

    float me_vst_plugin_get_parameter_value(void* plugin, int index)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->controller)
        {
            return 0.0f;
        }
        ParamID id = get_param_id(*nativePlugin, index);
        return static_cast<float>(nativePlugin->controller->getParamNormalized(id));
    }

    void me_vst_plugin_set_parameter_value(void* plugin, int index, float value)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->controller)
        {
            return;
        }
        ParamID id = get_param_id(*nativePlugin, index);
        nativePlugin->controller->setParamNormalized(id, value);
    }

    int me_vst_plugin_get_parameter_name(void* plugin, int index, char* buffer, int bufferSize)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->controller)
        {
            return 0;
        }

        ParameterInfo info {};
        if (nativePlugin->controller->getParameterInfo(index, info) != kResultTrue)
        {
            return 0;
        }

        return write_string(get_string128(info.title), buffer, bufferSize);
    }

    int me_vst_plugin_get_parameter_label(void* plugin, int index, char* buffer, int bufferSize)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->controller)
        {
            return 0;
        }

        ParameterInfo info {};
        if (nativePlugin->controller->getParameterInfo(index, info) != kResultTrue)
        {
            return 0;
        }

        return write_string(get_string128(info.units), buffer, bufferSize);
    }

    int me_vst_plugin_get_parameter_display(void* plugin, int index, char* buffer, int bufferSize)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->controller)
        {
            return 0;
        }

        ParamID id = get_param_id(*nativePlugin, index);
        String128 text {};
        auto value = nativePlugin->controller->getParamNormalized(id);
        if (nativePlugin->controller->getParamStringByValue(id, value, text) != kResultTrue)
        {
            return 0;
        }

        return write_string(get_string128(text), buffer, bufferSize);
    }

    void me_vst_plugin_process(void* plugin, float** inputs, float** outputs, int numInputs, int numOutputs, int sampleCount)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->processor)
        {
            return;
        }

        fill_event_list(*nativePlugin, sampleCount);
        setup_process_buffers(*nativePlugin, inputs, outputs, numInputs, numOutputs, sampleCount);

        nativePlugin->processor->process(nativePlugin->processData);
    }

    void me_vst_plugin_send_midi(void* plugin, int status, int data1, int data2)
    {
        me_vst_plugin_send_midi_at(plugin, status, data1, data2, 0);
    }

    void me_vst_plugin_send_midi_at(void* plugin, int status, int data1, int data2, int deltaFrames)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin)
        {
            return;
        }

        MidiEvent midi {};
        midi.status = status;
        midi.data1 = data1;
        midi.data2 = data2;
        midi.deltaFrames = deltaFrames;

        std::lock_guard<std::mutex> lock(nativePlugin->midiMutex);
        nativePlugin->pendingMidi.push_back(midi);
    }

    void me_vst_plugin_note_on(void* plugin, int channel, int note, int velocity)
    {
        int status = 0x90 | (channel & 0x0F);
        me_vst_plugin_send_midi(plugin, status, note, velocity);
    }

    void me_vst_plugin_note_off(void* plugin, int channel, int note)
    {
        int status = 0x80 | (channel & 0x0F);
        me_vst_plugin_send_midi(plugin, status, note, 0);
    }

    void me_vst_plugin_control_change(void* plugin, int channel, int controller, int value)
    {
        int status = 0xB0 | (channel & 0x0F);
        me_vst_plugin_send_midi(plugin, status, controller, value);
    }

    void me_vst_plugin_program_change(void* plugin, int channel, int program)
    {
        int status = 0xC0 | (channel & 0x0F);
        me_vst_plugin_send_midi(plugin, status, program, 0);
    }

    void me_vst_plugin_all_notes_off(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin)
        {
            return;
        }

        for (int channel = 0; channel < 16; ++channel)
        {
            me_vst_plugin_control_change(plugin, channel, kCtrlAllNotesOff, 0);
        }
    }

    void me_vst_plugin_clear_midi(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin)
        {
            return;
        }

        std::lock_guard<std::mutex> lock(nativePlugin->midiMutex);
        nativePlugin->pendingMidi.clear();
    }

    void me_vst_plugin_start_processing(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->processor)
        {
            return;
        }

        nativePlugin->processor->setProcessing(true);
    }

    void me_vst_plugin_stop_processing(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->processor)
        {
            return;
        }

        nativePlugin->processor->setProcessing(false);
    }

    void me_vst_plugin_suspend(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->component)
        {
            return;
        }

        nativePlugin->component->setActive(false);
    }

    void me_vst_plugin_resume(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->component)
        {
            return;
        }

        nativePlugin->component->setActive(true);
    }
    int me_vst_plugin_get_state_size(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->component)
        {
            return 0;
        }

        MemoryStream stream;
        if (nativePlugin->component->getState(&stream) != kResultTrue)
        {
            return 0;
        }

        return static_cast<int>(stream.getData().size());
    }

    int me_vst_plugin_get_state(void* plugin, unsigned char* buffer, int bufferSize)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->component || !buffer || bufferSize <= 0)
        {
            return 0;
        }

        MemoryStream stream;
        if (nativePlugin->component->getState(&stream) != kResultTrue)
        {
            return 0;
        }

        const auto& data = stream.getData();
        int toCopy = std::min<int>(bufferSize, static_cast<int>(data.size()));
        if (toCopy > 0)
        {
            std::memcpy(buffer, data.data(), static_cast<size_t>(toCopy));
        }
        return toCopy;
    }

    void me_vst_plugin_set_state(void* plugin, const unsigned char* buffer, int bufferSize)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->component)
        {
            return;
        }

        MemoryStream* stream = new MemoryStream();
        stream->setData(buffer, bufferSize);
        nativePlugin->component->setState(stream);
        stream->release();
    }

    int me_vst_plugin_get_program_count(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        return nativePlugin ? nativePlugin->programCount : 0;
    }

    int me_vst_plugin_get_current_program(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        return nativePlugin ? nativePlugin->currentProgram : 0;
    }

    void me_vst_plugin_set_current_program(void* plugin, int index)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin)
        {
            return;
        }
        nativePlugin->currentProgram = std::max(0, index);
    }

    int me_vst_plugin_get_program_name(void* plugin, int index, char* buffer, int bufferSize)
    {
        (void)plugin;
        (void)index;
        return write_string("", buffer, bufferSize);
    }

    int me_vst_plugin_load_preset(void* plugin, const char* path)
    {
        (void)plugin;
        (void)path;
        return 0;
    }

    int me_vst_plugin_save_preset(void* plugin, const char* path)
    {
        (void)plugin;
        (void)path;
        return 0;
    }

    void* me_vst_plugin_open_editor(void* plugin, void* parentWindow)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->controller)
        {
            return nullptr;
        }

        if (nativePlugin->plugView)
        {
            nativePlugin->isEditorOpen = true;
            return parentWindow;
        }

        auto view = owned(nativePlugin->controller->createView(ViewType::kEditor));
        if (!view)
        {
            return nullptr;
        }

        auto frame = owned(new SimplePlugFrame());
        view->setFrame(frame);

        if (view->attached(parentWindow, kPlatformTypeHWND) != kResultTrue)
        {
            view->setFrame(nullptr);
            return nullptr;
        }

        nativePlugin->plugView = view;
        nativePlugin->plugFrame = frame;
        nativePlugin->isEditorOpen = true;
        nativePlugin->hasEditor = true;
        return parentWindow;
    }

    void me_vst_plugin_close_editor(void* plugin)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->plugView)
        {
            return;
        }

        nativePlugin->plugView->removed();
        nativePlugin->plugView->setFrame(nullptr);
        nativePlugin->plugView = nullptr;
        nativePlugin->plugFrame = nullptr;
        nativePlugin->isEditorOpen = false;
    }

    int me_vst_plugin_get_editor_size(void* plugin, int* width, int* height)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin || !nativePlugin->plugView || !width || !height)
        {
            return 0;
        }

        ViewRect rect {};
        if (nativePlugin->plugView->getSize(&rect) != kResultTrue)
        {
            return 0;
        }

        *width = rect.getWidth();
        *height = rect.getHeight();
        return 1;
    }

    void me_vst_plugin_editor_idle(void* plugin)
    {
        (void)plugin;
    }

    void me_vst_plugin_set_transport(void* plugin, double tempo, double ppqPosition, int timeSigNumerator, int timeSigDenominator)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin)
        {
            return;
        }

        nativePlugin->processContext.tempo = tempo;
        nativePlugin->processContext.projectTimeMusic = ppqPosition;
        nativePlugin->processContext.timeSigNumerator = timeSigNumerator;
        nativePlugin->processContext.timeSigDenominator = timeSigDenominator;
    }

    void me_vst_plugin_set_transport_state(void* plugin, int isPlaying, int isRecording, int isLooping)
    {
        auto* nativePlugin = as_plugin(plugin);
        if (!nativePlugin)
        {
            return;
        }

        nativePlugin->processContext.state = 0;
        if (isPlaying)
        {
            nativePlugin->processContext.state |= ProcessContext::kPlaying;
        }
        if (isRecording)
        {
            nativePlugin->processContext.state |= ProcessContext::kRecording;
        }
        if (isLooping)
        {
            nativePlugin->processContext.state |= ProcessContext::kCycleActive;
        }
    }
}
