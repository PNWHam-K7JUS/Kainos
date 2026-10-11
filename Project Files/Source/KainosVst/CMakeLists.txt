# Kainos's VST3 host (KainosVst.dll), built on Steinberg's VST3 SDK (../vst3sdk, MIT licence)
cmake_minimum_required(VERSION 3.25)
project(KainosVst LANGUAGES CXX)

set(SMTG_ENABLE_VST3_PLUGIN_EXAMPLES OFF CACHE BOOL "" FORCE)
set(SMTG_ENABLE_VST3_HOSTING_EXAMPLES OFF CACHE BOOL "" FORCE)
set(SMTG_ENABLE_VSTGUI_SUPPORT OFF CACHE BOOL "" FORCE)
set(SMTG_CREATE_PLUGIN_LINK OFF CACHE BOOL "" FORCE)
set(SMTG_RUN_VST_VALIDATOR OFF CACHE BOOL "" FORCE)

add_subdirectory(${CMAKE_CURRENT_SOURCE_DIR}/../vst3sdk ${CMAKE_BINARY_DIR}/vst3sdk EXCLUDE_FROM_ALL)
if(NOT TARGET sdk_hosting)
    smtg_create_public_sdk_hosting_target()
endif()

set(VST3SDK ${CMAKE_CURRENT_SOURCE_DIR}/../vst3sdk)
# the Windows module loader and the plug provider (the SDK's hosting library leaves them to the host)
add_library(KainosVst SHARED kainosvst.cpp
    ${VST3SDK}/public.sdk/source/vst/hosting/module_win32.cpp
    ${VST3SDK}/public.sdk/source/vst/hosting/plugprovider.cpp)
target_link_libraries(KainosVst PRIVATE sdk_hosting)
target_compile_features(KainosVst PRIVATE cxx_std_17)
set_target_properties(KainosVst PROPERTIES OUTPUT_NAME "KainosVst")
