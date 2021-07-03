﻿using Prism.Ioc;
using WDE.Common.Database;
using WDE.Common.Windows;
using WDE.Module;
using WDE.Module.Attributes;
using WDE.MySqlDatabaseCommon.Database;
using WDE.MySqlDatabaseCommon.Database.World;
using WDE.MySqlDatabaseCommon.Services;
using WDE.MySqlDatabaseCommon.Tools;
using WDE.TrinityMySqlDatabase.Database;

namespace WDE.TrinityMySqlDatabase
{
    [AutoRegister]
    [SingleInstance]
    public class TrinityMySqlDatabaseModule : ModuleBase
    {
        public override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            base.RegisterTypes(containerRegistry);
            containerRegistry.RegisterSingleton<CachedDatabaseProvider>();
            containerRegistry.RegisterSingleton<NullWorldDatabaseProvider>();
            containerRegistry.RegisterSingleton<TrinityMySqlDatabaseProvider>();
            containerRegistry.RegisterSingleton<IMySqlExecutor, MySqlExecutor>();
            containerRegistry.RegisterSingleton<ICreatureStatCalculatorService, CreatureStatCalculatorService>();
            containerRegistry.RegisterSingleton<ICodeEditorViewModel, DebugQueryToolViewModel>();
        }
    }
}