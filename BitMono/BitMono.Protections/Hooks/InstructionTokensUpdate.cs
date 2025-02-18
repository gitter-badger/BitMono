﻿using dnlib.DotNet;

namespace BitMono.Protections.Hooks
{
    public class InstructionTokensUpdate
    {
        public MethodDef InitializatorMethodDef { get; set; }
        public MethodDef FromMethodDef { get; set; }
        public MethodDef ToMethodDef { get; set; }
        public int Index { get; set; }
    }
}