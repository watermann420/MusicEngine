# MusicEngine.VstBridge - Native VST Host Library
# CMake 3.20+ required for modern features

cmake_minimum_required(VERSION 3.20)

project(MusicEngine.VstBridge.Native
    VERSION 1.0.0
    DESCRIPTION "Native VST2/VST3 host bridge for MusicEngine"
    LANGUAGES CXX
)

# =============================================================================
# Configuration Options
# =============================================================================

option(VSTBRIDGE_BUILD_VST2 "Build with VST2 support" ON)
option(VSTBRIDGE_BUILD_VST3 "Build with VST3 support" OFF)
option(VSTBRIDGE_BUILD_TESTS "Build tests" OFF)

# VST3 SDK path (set this to enable VST3 support)
set(VST3_SDK_PATH "" CACHE PATH "Path to VST3 SDK")

# =============================================================================
# Compiler Settings
# =============================================================================

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Position independent code for shared library
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Windows-specific settings
if(WIN32)
    # Use static runtime to avoid dependency issues
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

    # Compiler flags
    add_compile_options(
        /W4             # Warning level 4
        /WX-            # Don't treat warnings as errors (for SDK compatibility)
        /MP             # Multi-processor compilation
        /Zi             # Debug information
        /EHsc           # C++ exception handling
        /permissive-    # Strict conformance mode
    )

    # Optimization flags for Release
    add_compile_options($<$<CONFIG:Release>:/O2>)
    add_compile_options($<$<CONFIG:Release>:/Oi>)
    add_compile_options($<$<CONFIG:Release>:/GL>)

    # Link-time optimization for Release
    add_link_options($<$<CONFIG:Release>:/LTCG>)

    # Export all symbols (for DLL)
    set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS OFF)
endif()

# =============================================================================
# Source Files
# =============================================================================

set(VSTBRIDGE_HEADERS
    native/include/vst_bridge.h
    native/include/vst2_host.h
    native/include/vst3_host.h
    native/include/audio_buffer.h
    native/include/midi_queue.h
)

set(VSTBRIDGE_SOURCES
    native/src/vst_bridge.cpp
    native/src/vst2_host.cpp
    native/src/vst3_host.cpp
    native/src/audio_buffer.cpp
    native/src/midi_queue.cpp
)

# =============================================================================
# Library Target
# =============================================================================

add_library(${PROJECT_NAME} SHARED
    ${VSTBRIDGE_HEADERS}
    ${VSTBRIDGE_SOURCES}
)

# Export macro
target_compile_definitions(${PROJECT_NAME} PRIVATE
    VSTBRIDGE_EXPORTS
    NOMINMAX            # Prevent Windows.h min/max macros
    WIN32_LEAN_AND_MEAN # Minimal Windows headers
)

# VST2 support
if(VSTBRIDGE_BUILD_VST2)
    target_compile_definitions(${PROJECT_NAME} PRIVATE VST2_SDK_AVAILABLE)
endif()

# VST3 support (requires SDK)
if(VSTBRIDGE_BUILD_VST3 AND VST3_SDK_PATH)
    if(EXISTS "${VST3_SDK_PATH}/pluginterfaces/vst/ivstaudioprocessor.h")
        target_compile_definitions(${PROJECT_NAME} PRIVATE VST3_SDK_AVAILABLE)
        target_include_directories(${PROJECT_NAME} PRIVATE
            "${VST3_SDK_PATH}"
            "${VST3_SDK_PATH}/pluginterfaces"
            "${VST3_SDK_PATH}/public.sdk"
        )
        message(STATUS "VST3 SDK found at: ${VST3_SDK_PATH}")
    else()
        message(WARNING "VST3 SDK path specified but SDK not found at: ${VST3_SDK_PATH}")
    endif()
endif()

# Include directories
target_include_directories(${PROJECT_NAME}
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/native/include>
        $<INSTALL_INTERFACE:include>
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/native/src
)

# Windows libraries
if(WIN32)
    target_link_libraries(${PROJECT_NAME} PRIVATE
        kernel32
        user32
        ole32
        oleaut32
        uuid
    )
endif()

# =============================================================================
# Output Settings
# =============================================================================

# Output directories
set_target_properties(${PROJECT_NAME} PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/build/$<IF:$<EQUAL:${CMAKE_SIZEOF_VOID_P},8>,x64,x86>"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/build/$<IF:$<EQUAL:${CMAKE_SIZEOF_VOID_P},8>,x64,x86>"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/build/$<IF:$<EQUAL:${CMAKE_SIZEOF_VOID_P},8>,x64,x86>"
)

# Rename output to match C# expectations
set_target_properties(${PROJECT_NAME} PROPERTIES
    OUTPUT_NAME "MusicEngine.VstBridge.Native"
)

# Debug postfix
set_target_properties(${PROJECT_NAME} PROPERTIES
    DEBUG_POSTFIX "d"
)

# =============================================================================
# Install Rules
# =============================================================================

install(TARGETS ${PROJECT_NAME}
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)

install(FILES ${VSTBRIDGE_HEADERS}
    DESTINATION include/vstbridge
)

# =============================================================================
# Copy to Managed Project (for development)
# =============================================================================

# Copy DLL to managed project output after build
add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        "$<TARGET_FILE:${PROJECT_NAME}>"
        "${CMAKE_CURRENT_SOURCE_DIR}/managed/runtimes/win-$<IF:$<EQUAL:${CMAKE_SIZEOF_VOID_P},8>,x64,x86>/native/"
    COMMENT "Copying native DLL to managed project"
)

# Create runtime directories if they don't exist
add_custom_command(TARGET ${PROJECT_NAME} PRE_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory
        "${CMAKE_CURRENT_SOURCE_DIR}/managed/runtimes/win-x64/native"
    COMMAND ${CMAKE_COMMAND} -E make_directory
        "${CMAKE_CURRENT_SOURCE_DIR}/managed/runtimes/win-x86/native"
    COMMENT "Creating runtime directories"
)

# =============================================================================
# Summary
# =============================================================================

message(STATUS "")
message(STATUS "MusicEngine.VstBridge.Native Configuration:")
message(STATUS "  Version:      ${PROJECT_VERSION}")
message(STATUS "  VST2 Support: ${VSTBRIDGE_BUILD_VST2}")
message(STATUS "  VST3 Support: ${VSTBRIDGE_BUILD_VST3}")
message(STATUS "  VST3 SDK:     ${VST3_SDK_PATH}")
message(STATUS "  Build Type:   ${CMAKE_BUILD_TYPE}")
message(STATUS "  C++ Standard: ${CMAKE_CXX_STANDARD}")
message(STATUS "")
