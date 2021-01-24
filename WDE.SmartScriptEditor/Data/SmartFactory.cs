﻿using System;
using System.Collections.Generic;
using WDE.Common.Database;
using WDE.Common.Parameters;
using WDE.Conditions.Data;
using WDE.Module.Attributes;
using WDE.SmartScriptEditor.Models;

namespace WDE.SmartScriptEditor.Data
{
    [AutoRegister]
    [SingleInstance]
    public class SmartFactory : ISmartFactory
    {
        private readonly IParameterFactory parameterFactory;
        private readonly ISmartDataManager smartDataManager;
        private readonly IConditionDataManager conditionDataManager;

        public SmartFactory(IParameterFactory parameterFactory, 
            ISmartDataManager smartDataManager, 
            IConditionDataManager conditionDataManager)
        {
            this.parameterFactory = parameterFactory;
            this.smartDataManager = smartDataManager;
            this.conditionDataManager = conditionDataManager;
        }

        public SmartEvent EventFactory(int id)
        {
            if (!smartDataManager.Contains(SmartType.SmartEvent, id))
                throw new NullReferenceException("No data for event id " + id);

            SmartEvent ev = new(id);
            ev.Chance.Value = 100;
            SmartGenericJsonData raw = smartDataManager.GetRawData(SmartType.SmartEvent, id);
            SetParameterObjects(ev, raw);
            return ev;
        }
        
        public void UpdateEvent(SmartEvent ev, int id)
        {
            if (ev.Id == id)
                return;

            SmartGenericJsonData raw = smartDataManager.GetRawData(SmartType.SmartEvent, id);
            SetParameterObjects(ev, raw, true);
        }
        
        public SmartEvent EventFactory(ISmartScriptLine line)
        {
            SmartEvent ev = EventFactory(line.EventType);

            ev.Chance.Value = line.EventChance;
            ev.Phases.Value = line.EventPhaseMask;
            ev.Flags.Value = line.EventFlags;
            ev.CooldownMin.Value = line.EventCooldownMin;
            ev.CooldownMax.Value = line.EventCooldownMax;

            for (var i = 0; i < SmartEvent.SmartEventParamsCount; ++i)
            {
                ev.GetParameter(i).Value = line.GetEventParam(i);
            }

            return ev;
        }

        public SmartCondition ConditionFactory(int id)
        {
            if (!conditionDataManager.HasConditionData(id))
                throw new NullReferenceException("No data for condition id " + id);

            SmartCondition ev = new(id);
            var raw = conditionDataManager.GetConditionData(id);
            SetParameterObjects(ev, raw);

            return ev;
        }

        public void UpdateCondition(SmartCondition smartCondition, int id)
        {
            if (smartCondition.Id == id)
                return;

            SetParameterObjects(smartCondition, conditionDataManager.GetConditionData(id));
        }

        public SmartCondition ConditionFactory(IConditionLine line)
        {
            SmartCondition condition = ConditionFactory(line.ConditionType);

            condition.Inverted.Value = line.NegativeCondition;
            condition.ConditionTarget.Value = line.ConditionTarget;
            condition.GetParameter(0).Value = line.ConditionValue1;
            condition.GetParameter(1).Value = line.ConditionValue2;
            condition.GetParameter(2).Value = line.ConditionValue3;

            return condition;
        }

        public SmartAction ActionFactory(int id, SmartSource source, SmartTarget target)
        {
            if (!smartDataManager.Contains(SmartType.SmartAction, id))
                throw new NullReferenceException("No data for action id " + id);

            SmartAction action = new(id, source, target);
            var raw = smartDataManager.GetRawData(SmartType.SmartAction, id);
            action.CommentParameter.IsUsed = id == SmartConstants.ActionComment;
            foreach (var t in action.Target.Position)
                t.IsUsed = raw.UsesTargetPosition;
            SetParameterObjects(action, raw);

            return action;
        }

        public void UpdateAction(SmartAction smartAction, int id)
        {
            if (smartAction.Id == id)
                return;
            
            SmartGenericJsonData raw = smartDataManager.GetRawData(SmartType.SmartAction, id);
            smartAction.CommentParameter.IsUsed = id == SmartConstants.ActionComment;
            foreach (var t in smartAction.Target.Position)
                t.IsUsed = raw.UsesTargetPosition;
            SetParameterObjects(smartAction, raw, true);
        }

        public SmartAction ActionFactory(ISmartScriptLine line)
        {
            SmartSource source = SourceFactory(line);
            SmartTarget target = TargetFactory(line);

            var raw = smartDataManager.GetRawData(SmartType.SmartAction, line.ActionType);

            if (raw.ImplicitSource != null)
                UpdateSource(source, smartDataManager.GetDataByName(SmartType.SmartSource, raw.ImplicitSource).Id);

            SmartAction action = ActionFactory(line.ActionType, source, target);

            for (var i = 0; i < SmartAction.SmartActionParametersCount; ++i)
                action.GetParameter(i).Value = line.GetActionParam(i);

            return action;
        }

        public SmartTarget TargetFactory(int id)
        {
            if (!smartDataManager.Contains(SmartType.SmartTarget, id))
                throw new NullReferenceException("No data for target id " + id);

            SmartTarget target = new(id);

            SetParameterObjects(target, smartDataManager.GetRawData(SmartType.SmartTarget, id));

            var targetTypes = smartDataManager.GetRawData(SmartType.SmartTarget, id).Types;

            if (targetTypes != null && targetTypes.Contains("Position"))
                target.IsPosition = true;

            return target;
        }

        public void UpdateTarget(SmartTarget smartTarget, int id)
        {
            if (smartTarget.Id == id)
                return;

            SmartGenericJsonData raw = smartDataManager.GetRawData(SmartType.SmartTarget, id);
            SetParameterObjects(smartTarget, raw, true);
        }

        public SmartSource SourceFactory(int id)
        {
            if (!smartDataManager.Contains(SmartType.SmartSource, id))
                throw new NullReferenceException("No data for source id " + id);

            SmartSource source = new(id);

            SetParameterObjects(source, smartDataManager.GetRawData(SmartType.SmartSource, id));

            return source;
        }
        
        public void UpdateSource(SmartSource smartSource, int id)
        {
            if (smartSource.Id == id)
                return;
            
            SmartGenericJsonData raw = smartDataManager.GetRawData(SmartType.SmartSource, id);
            SetParameterObjects(smartSource, raw, true);
        }
        
        public SmartTarget TargetFactory(ISmartScriptLine line)
        {
            SmartTarget target = TargetFactory(line.TargetType);

            target.X = line.TargetX;
            target.Y = line.TargetY;
            target.Z = line.TargetZ;
            target.O = line.TargetO;

            target.Condition.Value = (line.TargetConditionId);

            for (var i = 0; i < SmartSource.SmartSourceParametersCount; ++i)
                target.GetParameter(i).Value = line.GetTargetParam(i);

            return target;
        }

        private SmartSource SourceFactory(ISmartScriptLine line)
        {
            SmartSource source = SourceFactory(line.SourceType);

            source.Condition.Value = line.SourceConditionId;

            for (var i = 0; i < SmartSource.SmartSourceParametersCount; ++i)
                source.GetParameter(i).Value = line.GetSourceParam(i);

            return source;
        }

        private void SetParameterObjects(SmartBaseElement element, SmartGenericJsonData data, bool update = false)
        {
            if (data.DescriptionRules != null)
            {
                element.DescriptionRules = new List<DescriptionRule>();
                foreach (SmartDescriptionRulesJsonData rule in data.DescriptionRules)
                    element.DescriptionRules.Add(new DescriptionRule(rule));
            }
            else
                element.DescriptionRules = null;

            element.Id = data.Id;
            element.ReadableHint = data.Description;
            
            for (var i = 0; i < element.ParametersCount; ++i)
                element.GetParameter(i).IsUsed = false;
            
            if (data.Parameters == null)
                return;

            for (var i = 0; i < data.Parameters.Count; ++i)
            {
                string key = data.Parameters[i].Type;
                if (data.Parameters[i].Values != null)
                {
                    key = $"{data.Name}_{i}";
                    if (!parameterFactory.IsRegistered(key))
                        parameterFactory.Register(key, new Parameter(){Items = data.Parameters[i].Values});
                }
                
                IParameter<int> parameter = parameterFactory.Factory(key);
                element.GetParameter(i).Name = data.Parameters[i].Name;
                if (!update)
                    element.GetParameter(i).Value = data.Parameters[i].DefaultVal;
                element.GetParameter(i).Parameter = parameter;
                element.GetParameter(i).IsUsed = true;
            }
        }
        
        private void SetParameterObjects(SmartBaseElement element, ConditionJsonData data)
        {
            element.Id = data.Id;
            element.ReadableHint = data.Description;

            for (var i = 0; i < element.ParametersCount; ++i)
                element.GetParameter(i).IsUsed = false;

            if (data.Parameters == null)
                return;

            for (var i = 0; i < data.Parameters.Count; ++i)
            {
                string key = data.Parameters[i].Type;
                if (data.Parameters[i].Values != null)
                {
                    key = $"{data.Name}_{i}";
                    if (!parameterFactory.IsRegistered(key))
                        parameterFactory.Register(key, new Parameter(){Items = data.Parameters[i].Values});
                }
                
                IParameter<int> parameter = parameterFactory.Factory(key);

                element.GetParameter(i).Name = data.Parameters[i].Name;
                element.GetParameter(i).IsUsed = true;
                element.GetParameter(i).Parameter = parameter;
            }
        }
    }
}