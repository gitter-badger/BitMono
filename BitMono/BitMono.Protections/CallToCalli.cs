﻿using BitMono.API.Protecting;
using BitMono.API.Protecting.Contexts;
using BitMono.API.Protecting.Pipeline;
using BitMono.API.Protecting.Resolvers;
using BitMono.Core.Protecting.Analyzing.DnlibDefs;
using BitMono.Utilities.Extensions.dnlib;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ILogger = Serilog.ILogger;

namespace BitMono.Protections
{
    public class CallToCalli : IStageProtection
    {
        private readonly IDnlibDefFeatureObfuscationAttributeHavingResolver m_DnlibDefFeatureObfuscationAttributeHavingResolver;
        private readonly DnlibDefSpecificNamespaceHavingCriticalAnalyzer m_DnlibDefSpecificNamespaceHavingCriticalAnalyzer;
        private readonly DnlibDefCriticalAnalyzer m_DnlibDefCriticalAnalyzer;
        private readonly ILogger m_Logger;

        public CallToCalli(
            IDnlibDefFeatureObfuscationAttributeHavingResolver dnlibDefFeatureObfuscationAttributeHavingResolver,
            DnlibDefSpecificNamespaceHavingCriticalAnalyzer dnlibDefSpecificNamespaceHavingCriticalAnalyzer,
            DnlibDefCriticalAnalyzer dnlibDefCriticalAnalyzer,
            ILogger logger)
        {
            m_DnlibDefFeatureObfuscationAttributeHavingResolver = dnlibDefFeatureObfuscationAttributeHavingResolver;
            m_DnlibDefSpecificNamespaceHavingCriticalAnalyzer = dnlibDefSpecificNamespaceHavingCriticalAnalyzer;
            m_DnlibDefCriticalAnalyzer = dnlibDefCriticalAnalyzer;
            m_Logger = logger.ForContext<CallToCalli>();
        }

        public PipelineStages Stage => PipelineStages.ModuleWritten;

        public Task ExecuteAsync(ProtectionContext context, CancellationToken cancellationToken = default)
        {
            var moduleDefMD = ModuleDefMD.Load(context.BitMonoContext.OutputModuleFile, context.ModuleCreationOptions);
            context.ModuleDefMD = moduleDefMD;
            context.Importer = new Importer(moduleDefMD);

            var runtimeMethodHandle = context.Importer.Import(typeof(RuntimeMethodHandle));
            var getTypeFromHandleMethod = context.Importer.Import(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), new Type[]
            {
                typeof(RuntimeTypeHandle)
            }));
            var getModuleMethod = context.Importer.Import(typeof(Type).GetProperty(nameof(Type.Module)).GetMethod);
            var resolveMethodMethod = context.Importer.Import(typeof(Module).GetMethod(nameof(Module.ResolveMethod), new Type[]
            {
                typeof(int)
            }));
            var getMethodHandleMethod = context.Importer.Import(typeof(MethodBase).GetProperty(nameof(MethodBase.MethodHandle)).GetMethod);
            var getFunctionPointerMethod = context.Importer.Import(typeof(RuntimeMethodHandle).GetMethod(nameof(RuntimeMethodHandle.GetFunctionPointer)));

            foreach (var typeDef in context.ModuleDefMD.GetTypes().ToArray())
            {
                if (m_DnlibDefFeatureObfuscationAttributeHavingResolver.Resolve<CallToCalli>(typeDef))
                {
                    m_Logger.Information("Found {0}, skipping.", nameof(ObfuscationAttribute));
                    continue;
                }

                if (m_DnlibDefSpecificNamespaceHavingCriticalAnalyzer.NotCriticalToMakeChanges(typeDef) == false)
                {
                    m_Logger.Information("Not able to make changes because of specific namespace was found, skipping.");
                    continue;
                }

                foreach (var methodDef in typeDef.Methods.ToArray())
                {
                    if (methodDef.HasBody && methodDef.Body.HasInstructions
                        && methodDef.DeclaringType.IsGlobalModuleType == false
                        && methodDef.IsConstructor == false
                        && methodDef.NotGetterAndSetter()
                        && m_DnlibDefCriticalAnalyzer.NotCriticalToMakeChanges(methodDef))
                    {
                        if (m_DnlibDefFeatureObfuscationAttributeHavingResolver.Resolve<CallToCalli>(methodDef))
                        {
                            m_Logger.Information("Found {0}, skipping.", nameof(ObfuscationAttribute));
                            continue;
                        }

                        if (m_DnlibDefSpecificNamespaceHavingCriticalAnalyzer.NotCriticalToMakeChanges(methodDef) == false)
                        {
                            m_Logger.Information("Not able to make changes because of specific namespace was found, skipping.");
                            continue;
                        }

                        for (int i = 0; i < methodDef.Body.Instructions.Count; i++)
                        {
                            if (methodDef.Body.Instructions[i].OpCode == OpCodes.Call)
                            {
                                if (methodDef.Body.Instructions[i].Operand is MethodDef callingMethodDef && callingMethodDef.HasBody)
                                {
                                    var locals = methodDef.Body.Variables;
                                    var local = locals.Add(new Local(new ValueTypeSig(runtimeMethodHandle)));

                                    if (methodDef.Body.HasExceptionHandlers == false)
                                    {
                                        methodDef.Body.Instructions[i].OpCode = OpCodes.Nop;

                                        methodDef.Body.Instructions.Insert(i + 1, new Instruction(OpCodes.Ldtoken, context.ModuleDefMD.GlobalType));
                                        methodDef.Body.Instructions.Insert(i + 2, new Instruction(OpCodes.Call, getTypeFromHandleMethod));
                                        methodDef.Body.Instructions.Insert(i + 3, new Instruction(OpCodes.Callvirt, getModuleMethod));

                                        methodDef.Body.Instructions.Insert(i + 4, new Instruction(OpCodes.Ldc_I4, callingMethodDef.MDToken.ToInt32()));
                                        methodDef.Body.Instructions.Insert(i + 5, new Instruction(OpCodes.Call, resolveMethodMethod));
                                        methodDef.Body.Instructions.Insert(i + 6, new Instruction(OpCodes.Callvirt, getMethodHandleMethod));

                                        methodDef.Body.Instructions.Insert(i + 7, new Instruction(OpCodes.Stloc, local));
                                        methodDef.Body.Instructions.Insert(i + 8, new Instruction(OpCodes.Ldloca, local));

                                        methodDef.Body.Instructions.Insert(i + 9, new Instruction(OpCodes.Call, getFunctionPointerMethod));
                                        methodDef.Body.Instructions.Insert(i + 10, new Instruction(OpCodes.Calli, callingMethodDef.MethodSig));
                                        i += 10;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            using (context.ModuleDefMD)
            using (var fileStream = File.Open(context.BitMonoContext.OutputModuleFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                context.ModuleDefMD.Write(fileStream, context.ModuleWriterOptions);
            }
            return Task.CompletedTask;
        }
    }
}