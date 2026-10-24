cmake_minimum_required(VERSION 3.22.1)
project(unityLibrary CXX)

add_subdirectory(main)
add_subdirectory(fusion)

add_library(bpphistory SHARED history/history.cpp)
target_compile_options(bpphistory PRIVATE -Wall -Wextra -Werror)
