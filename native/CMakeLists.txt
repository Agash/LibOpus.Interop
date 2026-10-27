# Builds libopus as one shared library with the non-variadic control entry points (src/opus_interop.c)
# compiled in. eng/build-native.ps1 runs it against the pinned source in generate/inputs.json.
cmake_minimum_required(VERSION 3.21)
project(opus_interop C)

if(NOT OPUS_SOURCE_DIR)
  message(FATAL_ERROR "Set OPUS_SOURCE_DIR to the extracted libopus source.")
endif()

set(OPUS_BUILD_SHARED_LIBRARY ON CACHE BOOL "" FORCE)
set(OPUS_BUILD_TESTING OFF CACHE BOOL "" FORCE)
set(OPUS_BUILD_PROGRAMS OFF CACHE BOOL "" FORCE)
set(OPUS_INSTALL_PKG_CONFIG_MODULE OFF CACHE BOOL "" FORCE)
set(OPUS_INSTALL_CMAKE_CONFIG_MODULE OFF CACHE BOOL "" FORCE)
add_subdirectory(${OPUS_SOURCE_DIR} opus)

target_sources(opus PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/src/opus_interop.c)

# A name of its own, without a version in the file name or the SONAME: a process that has loaded the
# system's libopus.so.0 (through a distribution's FFmpeg or GStreamer) would otherwise be handed that
# copy by the dynamic loader, which matches libraries by SONAME, in place of this one.
set_target_properties(opus PROPERTIES OUTPUT_NAME opus-interop)
set_property(TARGET opus PROPERTY VERSION)
set_property(TARGET opus PROPERTY SOVERSION)
if(APPLE)
  set_target_properties(opus PROPERTIES INSTALL_NAME_DIR "@rpath")
endif()
