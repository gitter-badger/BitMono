﻿using BitMono.API.Configuration;
using BitMono.API.Protecting.Analyzing;
using BitMono.Core.Configuration.Extensions;
using BitMono.Utilities.Extensions.dnlib;
using dnlib.DotNet;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace BitMono.Core.Protecting.Analyzing.TypeDefs
{
    public class TypeDefCriticalBaseTypesCriticalAnalyzer : ICriticalAnalyzer<TypeDef>
    {
        private readonly IConfiguration m_Configuration;

        public TypeDefCriticalBaseTypesCriticalAnalyzer(IBitMonoCriticalsConfiguration configuration)
        {
            m_Configuration = configuration.Configuration;
        }

        public bool NotCriticalToMakeChanges(TypeDef typeDef)
        {
            if (typeDef.HasBaseType())
            {
                var criticalBaseTypes = m_Configuration.GetCriticalBaseTypes();
                if (criticalBaseTypes.FirstOrDefault(c => c.StartsWith(typeDef.BaseType.TypeName.Split('`')[0])) != null)
                {
                    return false;
                }
            }
            return true;
        }
    }
}