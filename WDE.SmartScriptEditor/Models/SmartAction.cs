﻿using System;
using System.ComponentModel;
using SmartFormat;
using SmartFormat.Core.Parsing;
using WDE.Common.Parameters;

namespace WDE.SmartScriptEditor.Models
{
    public class SmartAction : SmartBaseElement
    {
        public static readonly int SmartActionParametersCount = 6;
        
        private bool isSelected;

        private SmartEvent parent;

        private SmartSource source;

        private SmartTarget target;

        private ParameterValueHolder<string> comment;

        public SmartAction(int id, SmartSource source, SmartTarget target) : base(SmartActionParametersCount, id)
        {
            if (source == null || target == null)
                throw new ArgumentNullException("Source or target is null");

            this.source = source;
            this.target = target;
            source.OnChanged += SourceOnOnChanged;
            target.OnChanged += SourceOnOnChanged;
            comment = new ParameterValueHolder<string>("Comment", new StringParameter());
            comment.PropertyChanged += (_, _) =>
            {
                CallOnChanged();
                OnPropertyChanged(nameof(Comment));
            };
        }

        public SmartEvent Parent
        {
            get => parent;
            set
            {
                if (parent != null)
                    parent.PropertyChanged -= ParentPropertyChanged;
                parent = value;
                value.PropertyChanged += ParentPropertyChanged;
            }
        }

        public SmartSource Source => source;

        public bool IsSelected
        {
            get => isSelected || parent.IsSelected;
            set
            {
                if (value == isSelected)
                    return;
                isSelected = value;
                OnPropertyChanged();
            }
        }

        public string Comment
        {
            get => comment.Value;
            set
            {
                comment.Value = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Readable));
            }
        }

        public ParameterValueHolder<string> CommentParameter => comment;

        public SmartTarget Target => target;

        public override string Readable
        {
            get
            {
                try
                {
                    string output = Smart.Format(ReadableHint,
                        new
                        {
                            target = Target.Readable,
                            source = Source.Readable,
                            targetcoords = Target.GetCoords(),
                            target_position = Target.GetPosition(),
                            targetid = Target.Id,
                            sourceid = Source.Id,
                            pram1 = GetParameter(0),
                            pram2 = GetParameter(1),
                            pram3 = GetParameter(2),
                            pram4 = GetParameter(3),
                            pram5 = GetParameter(4),
                            pram6 = GetParameter(5),
                            datapram1 = "data #" + GetParameter(0).Value,
                            stored = "stored target #" + GetParameter(0).Value,
                            storedPoint = "stored point #" + GetParameter(0).Value,
                            timed1 = "timed event #" + GetParameter(0).Value,
                            timed4 = "timed event #" + GetParameter(0).Value,
                            function1 = "function #" + GetParameter(0).Value,
                            action1 = "action #" + GetParameter(0).Value,
                            pram1value = GetParameter(0).Value,
                            pram2value = GetParameter(1).Value,
                            pram3value = GetParameter(2).Value,
                            pram4value = GetParameter(3).Value,
                            pram5value = GetParameter(4).Value,
                            pram6value = GetParameter(5).Value,
                            comment = Comment
                        });
                    return output;
                }
                catch (ParsingErrors)
                {
                    return $"Action {Id} has invalid Readable format in actions.json";
                }
            }
        }

        private void ParentPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SmartEvent.IsSelected))
                //if (!_parent.IsSelected)
                //    IsSelected = false;
                OnPropertyChanged(nameof(IsSelected));
        }

        private void SourceOnOnChanged(object? sender, EventArgs e)
        {
            CallOnChanged();
        }

        public SmartAction Copy()
        {
            SmartAction se = new(Id, Source.Copy(), Target.Copy());
            se.ReadableHint = ReadableHint;
            se.DescriptionRules = DescriptionRules;
            for (var i = 0; i < SmartActionParametersCount; ++i)
                se.GetParameter(i).Copy(GetParameter(i));
            se.comment.Copy(comment);
            return se;
        }
    }
}