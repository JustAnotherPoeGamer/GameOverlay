﻿namespace AutoHotKeyTrigger.ProfileManager
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Numerics;
    using Conditions;
    using Conditions.DynamicCondition;
    using GameHelper.Utils;
    using ImGuiNET;
    using Newtonsoft.Json;
    using Enums;
    using GameHelper;
    using GameHelper.RemoteEnums;

    /// <summary>
    ///     Abstraction for the rule condition list
    /// </summary>
    public class Rule
    {
        private ConditionType newConditionType = ConditionType.AILMENT;
        private readonly Stopwatch cooldownStopwatch = Stopwatch.StartNew();

        [JsonProperty("Conditions", NullValueHandling = NullValueHandling.Ignore)]
        private readonly List<ICondition> conditions = new();

        [JsonProperty] private float delayBetweenRuns = 0;

        /// <summary>
        ///     Enable/Disable the rule.
        /// </summary>
        public bool Enabled;

        /// <summary>
        ///     User friendly name given to a rule.
        /// </summary>
        public string Name;

        /// <summary>
        ///     Rule key to press on success.
        /// </summary>
        public ConsoleKey Key;

        /// <summary>
        ///     Whether to use this rule as a trigger for auto-quit
        /// </summary>
        public bool UseAsAutoQuit;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Rule" /> class.
        /// </summary>
        /// <param name="name"></param>
        public Rule(string name)
        {
            this.Name = name;
        }

        /// <summary>
        ///     Creates default rules that are only valid for flasks on the newly created character.
        /// </summary>
        /// <returns>List of rules that are valid for newly created player.</returns>
        public static Rule[] CreateDefaultRules()
        {
            var rules = new Rule[6];
            for (var i = 0; i < 2; i++)
            {
                rules[i] = new($"SmallLifeFlask{i + 1}");
                rules[i].Enabled = true;
                rules[i].Key = ConsoleKey.D1 + i;
                rules[i].conditions.Add(new VitalsCondition(OperatorType.LESS_THAN, VitalType.LIFE_PERCENT, 70));
                rules[i].conditions.Add(new FlaskChargesCondition(OperatorType.BIGGER_THAN, i + 1, 6));
                rules[i].conditions.Add(new FlaskEffectCondition(i + 1));
            }

            rules[2] = new($"SmallLifeFlask3");
            rules[2].Enabled = false;
            rules[2].Key = ConsoleKey.D3;
            rules[2].conditions.Add(new VitalsCondition(OperatorType.LESS_THAN, VitalType.LIFE_PERCENT, 70));
            rules[2].conditions.Add(new FlaskChargesCondition(OperatorType.BIGGER_THAN, 3, 6));
            rules[2].conditions.Add(new FlaskEffectCondition(3));

            rules[3] = new("SmallManaFlask3");
            rules[3].Enabled = false;
            rules[3].Key = ConsoleKey.D3;
            rules[3].conditions.Add(new VitalsCondition(OperatorType.LESS_THAN, VitalType.MANA_PERCENT, 20));
            rules[3].conditions.Add(new FlaskChargesCondition(OperatorType.BIGGER_THAN, 3, 5));
            rules[3].conditions.Add(new FlaskEffectCondition(3));

            rules[4] = new($"QuickSilverFlask4");
            rules[4].Enabled = false;
            rules[4].Key = ConsoleKey.D4;
            rules[4].conditions.Add(new AnimationCondition(OperatorType.EQUAL_TO, Animation.Run, 1000));
            rules[4].conditions.Add(new FlaskChargesCondition(OperatorType.BIGGER_THAN, 4, 29));
            rules[4].conditions.Add(new FlaskEffectCondition(4));

            rules[5] = new("SmallManaFlask5");
            rules[5].Enabled = true;
            rules[5].Key = ConsoleKey.D5;
            rules[5].conditions.Add(new VitalsCondition(OperatorType.LESS_THAN, VitalType.MANA_PERCENT, 20));
            rules[5].conditions.Add(new FlaskChargesCondition(OperatorType.BIGGER_THAN, 5, 5));
            rules[5].conditions.Add(new FlaskEffectCondition(5));

            return rules;
        }

        /// <summary>
        ///     Clears the list of conditions
        /// </summary>
        public void Clear()
        {
            this.conditions.Clear();
        }

        /// <summary>
        ///     Adds a new condition to a rule
        /// </summary>
        /// <param name="condition">The condition to add</param>
        public void AddCondition(ICondition condition)
        {
            this.conditions.Add(condition);
        }

        /// <summary>
        ///     Displays the rule settings
        /// </summary>
        public void DrawSettings()
        {
            ImGui.Checkbox("Enable", ref this.Enabled);
            ImGui.SameLine();
            ImGui.Checkbox("Use as auto-quit", ref this.UseAsAutoQuit);
            ImGui.InputText("Name", ref this.Name, 20);
            if (!this.UseAsAutoQuit)
            {
                var tmpKey = (VirtualKeys)this.Key;
                if (ImGuiHelper.NonContinuousEnumComboBox("Key", ref tmpKey))
                {
                    this.Key = (ConsoleKey)tmpKey;
                }
                
                this.DrawCooldownWidget();
            }

            this.DrawAddNewCondition();
            this.DrawExistingConditions();
        }

        /// <summary>
        ///     Checks the rule conditions and presses its key if conditions are satisfied
        /// </summary>
        /// <param name="logger"></param>
        public void Execute(Action<string> logger)
        {
            if (this.Enabled && this.Evaluate())
            {
                if (this.UseAsAutoQuit)
                {
                    MiscHelper.KillTCPConnectionForProcess(Core.Process.Pid);
                }
                else
                {
                    if (MiscHelper.KeyUp(this.Key))
                    {
                        logger($"Pressed the {this.Key} key");
                        this.cooldownStopwatch.Restart();
                    }
                }
            }
        }

        /// <summary>
        ///     Adds a new condition
        /// </summary>
        /// <param name="conditionType"></param>
        private void Add(ConditionType conditionType)
        {
            var condition = EnumToObject(conditionType);
            if (condition != null)
            {
                this.AddCondition(condition);
            }
        }

        /// <summary>
        ///     Removes a condition at a specific index.
        /// </summary>
        /// <param name="index">index of the condition to remove.</param>
        private void RemoveAt(int index)
        {
            this.conditions.RemoveAt(index);
        }

        /// <summary>
        ///     Swap two conditions.
        /// </summary>
        /// <param name="i">index of the condition to swap.</param>
        /// <param name="j">index of the condition to swap.</param>
        private void Swap(int i, int j)
        {
            (this.conditions[i], this.conditions[j]) = (this.conditions[j], this.conditions[i]);
        }

        /// <summary>
        ///     Checks the specified conditions, shortcircuiting on the first unsatisfied one
        /// </summary>
        /// <returns>true if all the rules conditions are true otherwise false.</returns>
        public bool Evaluate()
        {
            if (this.UseAsAutoQuit || this.cooldownStopwatch.Elapsed.TotalSeconds > this.delayBetweenRuns)
            {
                if (this.conditions.TrueForAll(x => x.Evaluate()))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Converts the <see cref="ConditionType" /> to the appropriate <see cref="ICondition" /> object.
        /// </summary>
        /// <param name="conditionType">Condition type to create.</param>
        /// <returns>
        ///     Returns <see cref="ICondition" /> if user wants to create it or null.
        ///     Throws an exception in case it doesn't know how to create a specific Condition.
        /// </returns>
        private static ICondition EnumToObject(ConditionType conditionType)
        {
            ICondition p = conditionType switch
            {
                ConditionType.VITALS => VitalsCondition.Add(),
                ConditionType.ANIMATION => AnimationCondition.Add(),
                ConditionType.STATUS_EFFECT => StatusEffectCondition.Add(),
                ConditionType.FLASK_EFFECT => FlaskEffectCondition.Add(),
                ConditionType.FLASK_CHARGES => FlaskChargesCondition.Add(),
                ConditionType.AILMENT => AilmentCondition.Add(),
                ConditionType.DYNAMIC => DynamicCondition.Add(),
                _ => throw new Exception($"{conditionType} not implemented in ConditionHelper class")
            };

            return p;
        }

        private void DrawCooldownWidget()
        {
            ImGui.DragFloat("Cooldown time (seconds)##DelayTimerConditionDelay", ref this.delayBetweenRuns, 0.1f, 0.0f, 30.0f);
            ImGui.SameLine();
            var cooldownTimeFraction = this.delayBetweenRuns <= 0f ? 1f :
                MathF.Min((float)this.cooldownStopwatch.Elapsed.TotalSeconds, this.delayBetweenRuns) / this.delayBetweenRuns;
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, cooldownTimeFraction < 1f ?
                ImGuiHelper.Color(255, 0, 0, 255) : ImGuiHelper.Color(0, 255, 0, 255));
            ImGui.ProgressBar(
                (float)cooldownTimeFraction,
                new Vector2(ImGui.GetContentRegionAvail().X, 0f),
                cooldownTimeFraction < 1f ? "Cooling" : "Ready");
            ImGui.PopStyleColor();
        }

        private void DrawExistingConditions()
        {
            if (ImGui.TreeNode("Existing Conditions (all of them have to be true)"))
            {
                ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X / 6);
                for (var i = 0; i < this.conditions.Count; i++)
                {
                    ImGui.PushID($"ConditionNo{i}");
                    if (i != 0)
                    {
                        ImGui.Separator();
                    }

                    if (ImGui.Button("Delete"))
                    {
                        this.RemoveAt(i);
                        ImGui.PopID();
                        break;
                    }

                    ImGui.SameLine();
                    ImGui.BeginDisabled(i==0);
                    if (ImGui.ArrowButton("up", ImGuiDir.Up))
                    {
                        this.Swap(i, i - 1);
                        ImGui.PopID();
                        break;
                    }

                    ImGui.EndDisabled();
                    ImGui.SameLine();
                    ImGui.BeginDisabled(i == this.conditions.Count - 1);
                    if (ImGui.ArrowButton("down", ImGuiDir.Down))
                    {
                        this.Swap(i, i + 1);
                        ImGui.PopID();
                        break;
                    }

                    ImGui.EndDisabled();
                    ImGui.SameLine();
                    this.conditions[i].Display();
                    if (this.conditions[i] is not DynamicCondition)
                    {
                        ImGui.SameLine();
                        var evaluationResult = this.conditions[i].Evaluate();
                        ImGui.TextColored(
                            evaluationResult ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                            evaluationResult ? "(true)" : "(false)");
                    }

                    ImGui.PopID();
                }

                ImGui.PopItemWidth();
                ImGui.TreePop();
            }
        }

        private void DrawAddNewCondition()
        {
            if (ImGui.Button("Add New Condition"))
            {
                ImGui.OpenPopup("AddNewConditionPopUp");
            }

            if (ImGui.BeginPopup("AddNewConditionPopUp"))
            {
                ImGui.Text("NOTE: Click outside popup to close");
                ImGuiHelper.EnumComboBox("Condition", ref this.newConditionType);
                ImGui.Separator();
                this.Add(this.newConditionType);
                ImGui.EndPopup();
            }
        }
    }
}
