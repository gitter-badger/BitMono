﻿using dnlib.DotNet;

namespace BitMono.API.Protecting.Injection
{
    public interface IInjector
    {
        FieldDef InjectArrayInGlobalNestedTypes(ModuleDefMD moduleDefMD, byte[] injectedData, string injectedName);
        FieldDef InjectArray(ModuleDefMD moduleDefMD, TypeDef typeDef, byte[] injectedData, string injectedName);
        TypeDef CreateInvisibleType(ModuleDefMD moduleDefMD, string name = null);
        TypeDef CreateInvisibleValueType(ModuleDefMD moduleDefMD, string name = null);
        CustomAttribute InjectCompilerGeneratedAttribute(ModuleDefMD moduleDefMD, TypeDef typeDef = null);
        void InjectAttributeWithContent(ModuleDefMD moduleDefMD, string @namespace, string @name, string text);
        void InjectAttribute(ModuleDefMD moduleDefMD, string @namespace, string @name);
    }
}