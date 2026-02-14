cmake_minimum_required(VERSION 3.16)
project(MusicEngineCppLayerNative LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

if(NOT CMAKE_BUILD_TYPE)
  set(CMAKE_BUILD_TYPE Release)
endif()

set(VST3_SDK_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../../external/vst3sdk")

if(NOT EXISTS "${VST3_SDK_ROOT}/CMakeLists.txt")
  message(FATAL_ERROR "VST3 SDK not found at ${VST3_SDK_ROOT}. Run: git clone https://github.com/steinbergmedia/vst3sdk.git external/vst3sdk")
endif()

set(VST3_COMMON_SOURCES
  "${VST3_SDK_ROOT}/base/source/baseiids.cpp"
  "${VST3_SDK_ROOT}/base/source/fbuffer.cpp"
  "${VST3_SDK_ROOT}/base/source/fdebug.cpp"
  "${VST3_SDK_ROOT}/base/source/fdynlib.cpp"
  "${VST3_SDK_ROOT}/base/source/fobject.cpp"
  "${VST3_SDK_ROOT}/base/source/fstreamer.cpp"
  "${VST3_SDK_ROOT}/base/source/fstring.cpp"
  "${VST3_SDK_ROOT}/base/source/timer.cpp"
  "${VST3_SDK_ROOT}/base/source/updatehandler.cpp"
  "${VST3_SDK_ROOT}/base/thread/source/fcondition.cpp"
  "${VST3_SDK_ROOT}/base/thread/source/flock.cpp"
  "${VST3_SDK_ROOT}/pluginterfaces/base/coreiids.cpp"
  "${VST3_SDK_ROOT}/pluginterfaces/base/funknown.cpp"
  "${VST3_SDK_ROOT}/pluginterfaces/base/ustring.cpp"
  "${VST3_SDK_ROOT}/public.sdk/source/vst/utility/stringconvert.cpp"
  "${VST3_SDK_ROOT}/public.sdk/source/common/commoniids.cpp"
  "${VST3_SDK_ROOT}/public.sdk/source/common/commonstringconvert.cpp"
  "${VST3_SDK_ROOT}/public.sdk/source/vst/hosting/connectionproxy.cpp"
  "${VST3_SDK_ROOT}/public.sdk/source/vst/hosting/eventlist.cpp"
  "${VST3_SDK_ROOT}/public.sdk/source/vst/hosting/hostclasses.cpp"
  "${VST3_SDK_ROOT}/public.sdk/source/vst/hosting/module.cpp"
  "${VST3_SDK_ROOT}/public.sdk/source/vst/hosting/parameterchanges.cpp"
  "${VST3_SDK_ROOT}/public.sdk/source/vst/hosting/pluginterfacesupport.cpp"
  "${VST3_SDK_ROOT}/public.sdk/source/vst/hosting/plugprovider.cpp"
  "${VST3_SDK_ROOT}/public.sdk/source/vst/hosting/processdata.cpp"
  "${VST3_SDK_ROOT}/public.sdk/source/vst/vstinitiids.cpp"
)

if(WIN32)
  set(VST3_PLATFORM_SOURCES
    "${VST3_SDK_ROOT}/public.sdk/source/common/threadchecker_win32.cpp"
    "${VST3_SDK_ROOT}/public.sdk/source/vst/hosting/module_win32.cpp"
  )
elseif(APPLE)
  set(VST3_PLATFORM_SOURCES
    "${VST3_SDK_ROOT}/public.sdk/source/common/threadchecker_mac.mm"
    "${VST3_SDK_ROOT}/public.sdk/source/vst/hosting/module_mac.mm"
  )
else()
  set(VST3_PLATFORM_SOURCES
    "${VST3_SDK_ROOT}/public.sdk/source/common/threadchecker_linux.cpp"
    "${VST3_SDK_ROOT}/public.sdk/source/vst/hosting/module_linux.cpp"
  )
endif()

add_library(MusicEngine.CppLayer.Native SHARED
  Vst3Host.cpp
  ${VST3_COMMON_SOURCES}
  ${VST3_PLATFORM_SOURCES}
)

target_include_directories(MusicEngine.CppLayer.Native PRIVATE
  "${VST3_SDK_ROOT}"
  "${VST3_SDK_ROOT}/public.sdk"
  "${VST3_SDK_ROOT}/pluginterfaces"
  "${VST3_SDK_ROOT}/base/source"
  "${VST3_SDK_ROOT}/base/thread/include"
  "${VST3_SDK_ROOT}/public.sdk/source"
)

target_compile_definitions(MusicEngine.CppLayer.Native PRIVATE
  SMTG_USE_STL
  NOMINMAX
)

if(WIN32)
  target_compile_definitions(MusicEngine.CppLayer.Native PRIVATE WIN32 _WINDOWS)
endif()

set_target_properties(MusicEngine.CppLayer.Native PROPERTIES
  OUTPUT_NAME "MusicEngine.CppLayer.Native"
  LIBRARY_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/build/${CMAKE_BUILD_TYPE}"
  RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/build/${CMAKE_BUILD_TYPE}"
  ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/build/${CMAKE_BUILD_TYPE}"
)
