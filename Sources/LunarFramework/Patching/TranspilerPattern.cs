using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using LunarFramework.Logging;

namespace LunarFramework.Patching;

public class TranspilerPattern
{
    private static readonly HarmonyLogContext Logger = new(typeof(TranspilerPattern));

    public string Name { get; }

    internal bool IsGreedy { get; private set; }

    internal int MinOccurrences { get; private set; } = 1;
    internal int MaxOccurrences { get; private set; } = 1;

    internal List<Insertion> InsertBefore { get; } = [];
    internal List<RelativeMatchCondition> OnlyAfter { get; } = [];

    internal bool ShouldTrimBefore { get; private set; }
    internal bool ShouldTrimAfter { get; private set; }

    internal readonly List<Element> Elements = [];

    internal TranspilerPattern(string name)
    {
        Name = name;
    }

    public static TranspilerPattern Build(string name) => new(name);

    public static List<Occurrence> Find(IEnumerable<CodeInstruction> instructions, params TranspilerPattern[] patterns)
        => Find(instructions.ToList(), patterns);

    public static List<Occurrence> Find(List<CodeInstruction> instructions, params TranspilerPattern[] patterns)
    {
        var occurrences = new List<Occurrence>();

        var startIdx = 0;

        while (startIdx < instructions.Count)
        {
            foreach (var pattern in patterns)
            {
                if (pattern.Elements.Count > instructions.Count - startIdx) continue;
                if (!pattern.IsGreedy && occurrences.Count(o => o.Pattern == pattern) >= pattern.MinOccurrences) continue;
                if (pattern.OnlyAfter != null && !pattern.OnlyAfter.All(e => e.Match(occurrences, startIdx))) continue;
                if (!Enumerable.Range(0, pattern.Elements.Count).All(i => pattern.Elements[i].Matcher(instructions[startIdx + i]))) continue;
                var occurrence = new Occurrence(pattern, startIdx);
                Logger.Log($"Found transpiler pattern {occurrence}");
                startIdx += pattern.Elements.Count - 1;
                occurrences.Add(occurrence);
                break;
            }

            startIdx++;
        }

        foreach (var pattern in patterns)
        {
            var min = pattern.MinOccurrences;
            var max = pattern.MaxOccurrences;
            var count = occurrences.Count(o => o.Pattern == pattern);
            if (count < min)
                throw new Exception($"Transpiler pattern '{pattern.Name}' was expected to match at least {min} times, but matched {count} times");
            if (count > max)
                throw new Exception($"Transpiler pattern '{pattern.Name}' was expected to match at most {max} times, but matched {count} times");
        }

        return occurrences;
    }

    public static List<CodeInstruction> Apply(IEnumerable<CodeInstruction> instructions, params TranspilerPattern[] patterns)
        => Apply(instructions.ToList(), patterns);

    public static List<CodeInstruction> Apply(IEnumerable<CodeInstruction> instructions, ILGenerator generator, params TranspilerPattern[] patterns)
        => Apply(instructions.ToList(), generator, patterns);

    public static List<CodeInstruction> Apply(List<CodeInstruction> instructions, params TranspilerPattern[] patterns)
        => Apply(instructions, Find(instructions, patterns));

    public static List<CodeInstruction> Apply(List<CodeInstruction> instructions, ILGenerator generator, params TranspilerPattern[] patterns)
        => Apply(instructions, generator, Find(instructions, patterns));

    public static List<CodeInstruction> Apply(IEnumerable<CodeInstruction> instructions, List<Occurrence> occurrences)
        => Apply(instructions.ToList(), occurrences);

    public static List<CodeInstruction> Apply(IEnumerable<CodeInstruction> instructions, ILGenerator generator, List<Occurrence> occurrences)
        => Apply(instructions.ToList(), generator, occurrences);

    public static List<CodeInstruction> Apply(List<CodeInstruction> instructions, List<Occurrence> occurrences)
        => Apply(instructions, null, occurrences);

    public static List<CodeInstruction> Apply(List<CodeInstruction> instructions, ILGenerator generator, List<Occurrence> occurrences)
    {
        if (occurrences.Count == 0) return instructions.ToList();

        var occIdx = 0;

        var result = new List<CodeInstruction>();
        var labels = new Dictionary<string, Label>();
        var passed = new Dictionary<TranspilerPattern, int>();

        for (int idxInBase = 0; idxInBase < instructions.Count; idxInBase++)
        {
            var occurrence = occurrences[occIdx];
            var baseInstruction = instructions[idxInBase];
            var idxInPattern = idxInBase - occurrence.StartIndex;

            passed.TryGetValue(occurrence.Pattern, out var passedCount);

            if (idxInPattern < 0)
            {
                if (occurrence.Pattern.ShouldTrimBefore)
                {
                    occurrence.Pattern.CheckCanRemove(baseInstruction);
                }
                else if (occIdx > 0 && occurrences[occIdx - 1].Pattern.ShouldTrimAfter)
                {
                    occurrences[occIdx - 1].Pattern.CheckCanRemove(baseInstruction);
                }
                else
                {
                    result.Add(baseInstruction);
                }
            }
            else if (idxInPattern >= occurrence.Pattern.Elements.Count)
            {
                if (occurrence.Pattern.ShouldTrimAfter)
                {
                    occurrence.Pattern.CheckCanRemove(baseInstruction);
                }
                else
                {
                    result.Add(baseInstruction);
                }
            }
            else
            {
                var element = occurrence.Pattern.Elements[idxInPattern];

                if (idxInPattern == 0)
                {
                    foreach (var insertion in occurrence.Pattern.InsertBefore)
                    {
                        var newInstruction = insertion.Instruction.Clone();
                        result.Add(newInstruction);

                        if (newInstruction.operand is LabelPlaceholder placeholder)
                            newInstruction.operand = GetOrCreateLabel(placeholder.label, passedCount + 1);

                        foreach (var labelId in insertion.Labels)
                            newInstruction.labels.Add(GetOrCreateLabel(labelId, passedCount + 1));
                    }
                }

                var resultInstruction = element.Action != null ? element.Action(baseInstruction) : baseInstruction;
                if (resultInstruction != null)
                {
                    result.Add(resultInstruction);

                    if (resultInstruction.operand is LabelPlaceholder placeholder)
                        resultInstruction.operand = GetOrCreateLabel(placeholder.label, passedCount + 1);

                    foreach (var labelId in element.Labels)
                        resultInstruction.labels.Add(GetOrCreateLabel(labelId, passedCount + 1));
                }

                foreach (var insertion in element.InsertAfter)
                {
                    var newInstruction = insertion.Instruction.Clone();
                    result.Add(newInstruction);

                    if (newInstruction.operand is LabelPlaceholder placeholder)
                        newInstruction.operand = GetOrCreateLabel(placeholder.label, passedCount + 1);

                    foreach (var labelId in insertion.Labels)
                        newInstruction.labels.Add(GetOrCreateLabel(labelId, passedCount + 1));
                }

                if (idxInPattern >= occurrence.Pattern.Elements.Count - 1)
                {
                    passed[occurrence.Pattern] = passedCount + 1;
                    if (occIdx < occurrences.Count - 1) occIdx++;
                    Logger.Log($"Applied transpiler pattern {occurrence}");
                }
            }
        }

        return result;

        Label GetOrCreateLabel(string id, int idx)
        {
            if (generator == null) throw new Exception("no IL generator given");
            var labelId = id.Contains("#") ? id : id + "#" + idx;
            if (!labels.TryGetValue(labelId, out var label)) label = labels[labelId] = generator.DefineLabel();
            return label;
        }
    }

    public Element Match(Predicate<CodeInstruction> matcher)
    {
        var element = new Element(this, matcher);
        Elements.Add(element);
        return element;
    }

    public Element MatchAny() => Match(_ => true);

    public Element Match(OpCode opcode) => Match(ci => Equals(ci.opcode, opcode));

    public Element Match(OpCode opcode, object operand) => Match(ci => Equals(ci.opcode, opcode) && Equals(ci.operand, operand));

    public Element Match(CodeInstruction match) => Match(ci => Equals(ci.opcode, match.opcode) && Equals(ci.operand, match.operand));

    public Element MatchConst(long val) => Match(ci => ci.LoadsConstant(val));

    public Element MatchConst(double val) => Match(ci => ci.LoadsConstant(val));

    public Element MatchConst(string str) => Match(ci => ci.LoadsConstant(str));

    public Element MatchLdarg(int? n = null) => Match(ci => ci.IsLdarg(n));

    public Element MatchStarg(int? n = null) => Match(ci => ci.IsStarg(n));

    public Element MatchLdloc(LocalBuilder local = null) => Match(ci => ci.IsLdloc(local));

    public Element MatchStloc(LocalBuilder local = null) => Match(ci => ci.IsStloc(local));

    public Element MatchCall(MethodInfo methodInfo) => Match(ci => ci.Calls(methodInfo));

    public Element MatchLoad(FieldInfo fieldInfo) => Match(ci => ci.LoadsField(fieldInfo));

    public Element MatchStore(FieldInfo fieldInfo) => Match(ci => ci.StoresField(fieldInfo));

    public Element MatchCall(Type type, string methodName, Type[] parameters = null, Type[] generics = null)
    {
        var methodInfo = AccessTools.Method(type, methodName, parameters, generics);
        if (methodInfo == null) throw new Exception($"Method {type.FullName}.{methodName} not found");
        return MatchCall(methodInfo);
    }

    public Element MatchLoad(Type type, string fieldName)
    {
        var fieldInfo = AccessTools.Field(type, fieldName);
        if (fieldInfo == null) throw new Exception($"Field {type.FullName}.{fieldName} not found");
        return MatchLoad(fieldInfo);
    }

    public Element MatchStore(Type type, string fieldName)
    {
        var fieldInfo = AccessTools.Field(type, fieldName);
        if (fieldInfo == null) throw new Exception($"Field {type.FullName}.{fieldName} not found");
        return MatchStore(fieldInfo);
    }

    public Element MatchNewobj(Type type, Type[] parameters = null)
    {
        var constructorInfo = AccessTools.Constructor(type, parameters);
        if (constructorInfo == null) throw new Exception($"Constructor for {type.FullName} not found");
        return Match(ci => ci.opcode == OpCodes.Newobj && Equals(ci.operand, constructorInfo));
    }

    public TranspilerPattern InsertCall(Type type, string name, Type[] parameters = null, Type[] generics = null)
        => Insert(CodeInstruction.Call(type, name, parameters, generics));

    public TranspilerPattern InsertLoadField(Type type, string name, bool useAddress = false)
        => Insert(CodeInstruction.LoadField(type, name, useAddress));

    public TranspilerPattern Insert(OpCode opcode, object operand = null) => Insert(new CodeInstruction(opcode, operand));

    public TranspilerPattern Insert(CodeInstruction instruction)
    {
        var insertion = new Insertion { Instruction = instruction, Labels = [] };

        if (Elements.Count > 0)
        {
            Elements.Last().InsertAfter.Add(insertion);
        }
        else
        {
            InsertBefore.Add(insertion);
        }

        return this;
    }

    public TranspilerPattern TrimBefore(bool trimBefore = true)
    {
        ShouldTrimBefore = trimBefore;
        return this;
    }

    public TranspilerPattern TrimAfter(bool trimAfter = true)
    {
        ShouldTrimAfter = trimAfter;
        return this;
    }

    public TranspilerPattern Greedy(int minOccurrences = 1, int maxOccurrences = int.MaxValue)
    {
        IsGreedy = true;
        MinOccurrences = minOccurrences;
        MaxOccurrences = maxOccurrences;
        return this;
    }

    public TranspilerPattern Lazy(int occurrences = 1)
    {
        IsGreedy = false;
        MinOccurrences = occurrences;
        MaxOccurrences = occurrences;
        return this;
    }

    public TranspilerPattern OnlyMatchBefore(TranspilerPattern other)
        => OnlyMatchAfter(other, 0, 0);

    public TranspilerPattern OnlyMatchAfter(TranspilerPattern other, int minCount = 1, int maxCount = int.MaxValue)
        => OnlyMatchAfter(other, minCount, maxCount, 0, int.MaxValue);

    public TranspilerPattern OnlyMatchDirectlyAfter(TranspilerPattern other)
        => OnlyMatchAfter(other, 1, int.MaxValue, 0, 0);

    public TranspilerPattern OnlyMatchAfter(
        TranspilerPattern other,
        int minCount, int maxCount,
        int minDistance, int maxDistance)
    {
        OnlyAfter.RemoveAll(e => e.Pattern == other);
        OnlyAfter.Add(new RelativeMatchCondition(other, minCount, maxCount, minDistance, maxDistance));
        return this;
    }

    public TranspilerPattern PutLabel(string label)
    {
        if (Elements.Count > 0)
        {
            var element = Elements.Last();
            if (element.InsertAfter.Count > 0)
            {
                element.InsertAfter.Last().Labels.Add(label);
            }
            else
            {
                element.Labels.Add(label);
            }
        }
        else if (InsertBefore.Count > 0)
        {
            InsertBefore.Last().Labels.Add(label);
        }
        else
        {
            throw new Exception("no target for label");
        }

        return this;
    }

    public static object Label(string label)
    {
        return new LabelPlaceholder { label = label };
    }

    internal void CheckCanRemove(CodeInstruction ci)
    {
        if (ci.labels.Count > 0 || ci.blocks.Count > 0)
        {
            Logger.Warn($"Removed instruction {ci} had labels or blocks assigned!");
        }
    }

    internal static string CodePos(int idx) => $"{idx:X4}";

    public class Element
    {
        public readonly TranspilerPattern Pattern;

        internal Predicate<CodeInstruction> Matcher;
        internal Func<CodeInstruction, CodeInstruction> Action;
        internal List<Insertion> InsertAfter = [];
        internal List<string> Labels = [];

        internal Element(TranspilerPattern pattern, Predicate<CodeInstruction> matcher = null)
        {
            Pattern = pattern;
            Matcher = matcher;
        }

        public TranspilerPattern Do(Func<CodeInstruction, CodeInstruction> action)
        {
            var prevAction = Action;
            Action = prevAction == null ? action : ci => action(prevAction(ci));
            return Pattern;
        }

        public TranspilerPattern Do(Action<CodeInstruction> action)
        {
            return Do(ci =>
            {
                action(ci);
                return ci;
            });
        }

        public TranspilerPattern Keep()
        {
            return Pattern;
        }

        public TranspilerPattern Replace(OpCode opcode, object operand = null) => Do(ci =>
        {
            ci.opcode = opcode;
            ci.operand = operand;
        });

        public TranspilerPattern ReplaceOperand(object operand) => Do(ci =>
        {
            ci.operand = operand;
        });

        public TranspilerPattern ReplaceOperandWithField(Type type, string name)
            => ReplaceOperand(CodeInstruction.LoadField(type, name).operand);

        public TranspilerPattern ReplaceOperandWithMethod(Type type, string name, Type[] parameters = null, Type[] generics = null)
            => ReplaceOperand(CodeInstruction.Call(type, name, parameters, generics).operand);

        public TranspilerPattern Remove() => Do(ci =>
        {
            Pattern.CheckCanRemove(ci);
            return null;
        });

        public TranspilerPattern Nop() => Replace(OpCodes.Nop);

        public Element DoOnMatch(Action<CodeInstruction> action)
        {
            var matcher = Matcher;

            Matcher = ci =>
            {
                if (!matcher(ci)) return false;
                action(ci);
                return true;
            };

            return this;
        }

        public Element StoreIn(CodeInstruction dest) => DoOnMatch(ci =>
        {
            dest.opcode = ci.opcode;
            dest.operand = ci.operand;
            dest.labels = ci.labels.ToList();
            dest.blocks = ci.blocks.ToList();
            Logger.Log($"Stored instruction {ci}");
        });

        public Element StoreOperandIn(params CodeInstruction[] dest) => DoOnMatch(ci =>
        {
            foreach (var dci in dest)
            {
                if (LdlocToStloc.TryGetValue(ci.opcode, out var opSt))
                {
                    if (StlocToLdloc.ContainsKey(dci.opcode)) dci.opcode = opSt;
                    else if (LdlocToStloc.ContainsKey(dci.opcode)) dci.opcode = ci.opcode;
                }
                else if (StlocToLdloc.TryGetValue(ci.opcode, out var opLd))
                {
                    if (LdlocToStloc.ContainsKey(dci.opcode)) dci.opcode = opLd;
                    else if (StlocToLdloc.ContainsKey(dci.opcode)) dci.opcode = ci.opcode;
                }

                dci.operand = ci.operand;
                Logger.Log($"Stored operand from instruction {ci} to {dci}");
            }
        });
    }

    public readonly struct Occurrence
    {
        public readonly TranspilerPattern Pattern;

        /// <summary>
        /// The start index (inclusive) pointing to the first instruction in the matched section.
        /// </summary>
        public readonly int StartIndex;

        /// <summary>
        /// The instruction count in the matched section.
        /// </summary>
        public int Length => Pattern.Elements.Count;

        /// <summary>
        /// The end index (exclusive) pointing to the first instruction after the end of the matched section.
        /// </summary>
        public int EndIndex => StartIndex + Length;

        public Occurrence(TranspilerPattern pattern, int startIndex)
        {
            Pattern = pattern;
            StartIndex = startIndex;
        }

        public override string ToString()
        {
            return $"{Pattern.Name}@[{CodePos(StartIndex)}-{CodePos(EndIndex)}]";
        }
    }

    internal readonly struct RelativeMatchCondition
    {
        public readonly TranspilerPattern Pattern;
        public readonly int MinCount;
        public readonly int MaxCount;
        public readonly int MinDistance;
        public readonly int MaxDistance;

        public bool Match(List<Occurrence> occurrences, int startIdx)
        {
            var pattern = Pattern;
            var count = occurrences.Count(o => o.Pattern == pattern);
            if (count < MinCount || count > MaxCount) return false;
            if (MinDistance <= 0 && MaxDistance == int.MaxValue) return true;
            var distance = startIdx - occurrences.Where(o => o.Pattern == pattern).Max(o => o.EndIndex);
            return distance >= MinDistance && distance <= MaxDistance;
        }

        public RelativeMatchCondition(
            TranspilerPattern pattern,
            int minCount, int maxCount,
            int minDistance, int maxDistance)
        {
            Pattern = pattern;
            MinCount = minCount;
            MaxCount = maxCount;
            MinDistance = minDistance;
            MaxDistance = maxDistance;
        }
    }

    internal struct Insertion
    {
        public CodeInstruction Instruction;
        public List<string> Labels;
    }

    internal class LabelPlaceholder
    {
        public string label;
    }

    internal static readonly Dictionary<OpCode, OpCode> LdlocToStloc = new()
    {
        { OpCodes.Ldloc, OpCodes.Stloc },
        { OpCodes.Ldloc_S, OpCodes.Stloc_S },
        { OpCodes.Ldloc_0, OpCodes.Stloc_0 },
        { OpCodes.Ldloc_1, OpCodes.Stloc_1 },
        { OpCodes.Ldloc_2, OpCodes.Stloc_2 },
        { OpCodes.Ldloc_3, OpCodes.Stloc_3 }
    };

    internal static readonly Dictionary<OpCode, OpCode> StlocToLdloc = LdlocToStloc.ToDictionary(e => e.Value, e => e.Key);
}
