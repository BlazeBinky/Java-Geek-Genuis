# Experimental AmigaOS 4 PPC cross-toolchain for the modern Odyssey branch.
# Based on the historical upstream OS4 compile branch; not yet validated against
# the newer source tree. Override SDK/include paths with environment variables.
set(CMAKE_SYSTEM_NAME Generic)
set(CMAKE_SYSTEM_VERSION 1)
set(CMAKE_SYSTEM_PROCESSOR ppc)
set(CMAKE_CROSSCOMPILING ON)

if (NOT DEFINED CMAKE_C_COMPILER)
    set(CMAKE_C_COMPILER ppc-amigaos-gcc CACHE FILEPATH "AmigaOS 4 C compiler")
endif ()
if (NOT DEFINED CMAKE_CXX_COMPILER)
    set(CMAKE_CXX_COMPILER ppc-amigaos-g++ CACHE FILEPATH "AmigaOS 4 C++ compiler")
endif ()

if (DEFINED ENV{ODYSSEY_OPT_LEVEL})
    set(_odyssey_opt_level "$ENV{ODYSSEY_OPT_LEVEL}")
else ()
    set(_odyssey_opt_level "2")
endif ()
if (NOT _odyssey_opt_level MATCHES "^[23]$")
    message(FATAL_ERROR "ODYSSEY_OPT_LEVEL must be 2 or 3")
endif ()

if (DEFINED ENV{AMIGAOS4_MUI_INCLUDE_DIR})
    set(_amigaos4_mui_include "$ENV{AMIGAOS4_MUI_INCLUDE_DIR}")
elseif (DEFINED ENV{AMIGAOS4_SDK_ROOT})
    if (IS_DIRECTORY "$ENV{AMIGAOS4_SDK_ROOT}/MUI/C/include")
        set(_amigaos4_mui_include "$ENV{AMIGAOS4_SDK_ROOT}/MUI/C/include")
    else ()
        # AmigaGCConDocker installs MUI 5 headers under local/common/include.
        set(_amigaos4_mui_include "$ENV{AMIGAOS4_SDK_ROOT}/local/common/include")
    endif ()
elseif (IS_DIRECTORY "/opt/ppc-amigaos/ppc-amigaos/SDK/local/common/include")
    set(_amigaos4_mui_include "/opt/ppc-amigaos/ppc-amigaos/SDK/local/common/include")
else ()
    set(_amigaos4_mui_include "/usr/ppc-amigaos/SDK/MUI/C/include")
endif ()

set(_amigaos4_common_flags "-D__USE_INLINE__ -Wno-expansion-to-defined -D_GNU_SOURCE")
if (IS_DIRECTORY "${_amigaos4_mui_include}")
    string(APPEND _amigaos4_common_flags " -I${_amigaos4_mui_include}")
else ()
    message(WARNING "MUI C include directory not found at '${_amigaos4_mui_include}'. Set AMIGAOS4_MUI_INCLUDE_DIR.")
endif ()

if (DEFINED ENV{AMIGAOS4_CPU_FLAGS} AND NOT "$ENV{AMIGAOS4_CPU_FLAGS}" STREQUAL "")
    string(APPEND _amigaos4_common_flags " $ENV{AMIGAOS4_CPU_FLAGS}")
endif ()

# Keep Release builds optimized, while allowing an experimental -O3 profile.
# Do not hard-code a G4/G5 tuning flag: OS4 PPC hardware varies across systems.
set(CMAKE_C_FLAGS "${_amigaos4_common_flags}" CACHE STRING "AmigaOS 4 C flags" FORCE)
set(CMAKE_CXX_FLAGS "${_amigaos4_common_flags}" CACHE STRING "AmigaOS 4 C++ flags" FORCE)
set(CMAKE_C_FLAGS_RELEASE "-O${_odyssey_opt_level} -DNDEBUG" CACHE STRING "AmigaOS 4 Release C flags" FORCE)
set(CMAKE_CXX_FLAGS_RELEASE "-O${_odyssey_opt_level} -DNDEBUG" CACHE STRING "AmigaOS 4 Release C++ flags" FORCE)

set(CMAKE_MODULE_LINKER_FLAGS "" CACHE STRING "AmigaOS 4 module linker flags" FORCE)

# Mirrors the historical upstream OS4 build setup. Libraries must be target
# libraries from the AmigaOS SDK, never host x86_64 Linux libraries.
set(CMAKE_FIND_ROOT_PATH "")
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
