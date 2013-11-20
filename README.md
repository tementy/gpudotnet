gpudotnet
=========

A simple CIL-to-GPU translator targeting PTX/AMDIL for NVIDIA/AMD GPUs.
You write your GPGPU code using pure C# and launch the kernel via OpenCL
interface (subset of https://openclnet.codeplex.com codebase is used here).
Translation pipeline relies on .NET introspection (Mono.Reflection disassembler
by Jb Evain is used here).

Translation pipeline starts from ordinary .NET compiler and continues with 5
runtime phases:

1. Disassembling with Mono.Reflection namespace functions.
2. Instructions selection (intermediate representation construction).
3. Optional IR-based optimization: subprograms inlining.
4. Target code generation: IR->PTX for NVIDIA or IR->AMDIL for AMD.
5. Final compilation with OpenCL functions from OpenCL.Net namespace.

This project was originally developed under master thesis research.
The Author hopes this code may be useful in educational purposes.
