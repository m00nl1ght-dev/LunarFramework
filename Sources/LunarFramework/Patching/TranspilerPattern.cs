using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using LunarFramework.Logging;

namespace LunarFramework.Patching;

public class TranspilerPattern
{
    private static readonly HarmonyLogContext Logger = new(typeof(TranspilerPattern));
    
    public string Name { get; }

    protected internal bool IsGreedy { get; private set; }

    protected internal int MinOccurences { get; private set; } = 1;
    protected internal int MaxOccurences { get; private set; } = 1;

    protected internal List<CodeInstruction> InsertBefore { get; set; }
    protected internal List<RelativeMatchCondition> OnlyAfter { get; set; }

    protected internal bool ShouldTrimBefore { get; private set; }
    protected internal bool ShouldTrimAfter { get; private set; }

    protected readonly List<Element> Elements = new();

    internal TranspilerPattern(string name)
    {
        Name = name;
    }

    public static TranspilerPattern Build(string name) => new(name);

    public static List<Occurence> Find(IEnumerable<CodeInstruction> instructions, params TranspilerPattern[] patterns)
        => Find(instructions.ToList(), patterns);

    public static List<Occurence> Find(List<CodeInstruction> instructions, params TranspilerPattern[] patterns)
    {
        var occurences = new List<Occurence>();

        var startIdx = 0;

        while (startIdx < instructions.Count)
        {
            foreach (var pattern in patterns)
            {
                if (pattern.Elements.Count > instructions.Count - startIdx) continue;
                if (!pattern.IsGreedy && occurences.Count(o => o.Pattern == pattern) >= pattern.MinOccurences) continue;
                if (pattern.OnlyAfter != null && !pattern.OnlyAfter.All(e => e.InRange(occurences.Count(o => o.Pattern == e.Pattern)))) continue;
                if (!Enumerable.Range(0, pattern.Elements.Count).All(i => pattern.Elements[i].Matcher(instructions[startIdx + i]))) continue;
                var occurence = new Occurence(pattern, startIdx);
                Logger.Log($"Found transpiler pattern {occurence}");
                startIdx += pattern.Elements.Count - 1;
                occurences.Add(occurence);
                break;
            }

            startIdx++;
        }

        foreach (var pattern in patterns)
        {
            var min = pattern.MinOccurences;
            var max = pattern.MaxOccurences;
            var count = occurences.Count(o => o.Pattern == pattern);
            if (count < min)
                throw new Exception($"Transpiler pattern '{pattern.Name}' was expected to match at least {min} times, but matched {count} times");
            if (count > max)
                throw new Exception($"Transpiler pattern '{pattern.Name}' was expected to match at most {max} times, but matched {count} times");
        }

        return occurences;
    }

    public static List<CodeInstruction> Apply(IEnumerable<CodeInstruction> instructions, params TranspilerPattern[] patterns)
        => Apply(instructions.ToList(), patterns);

    public static List<CodeInstruction> Apply(List<CodeInstruction> instructions, params TranspilerPattern[] patterns)
        => Apply(instructions, Find(instructions, patterns));

    public static List<CodeInstruction> Apply(IEnumerable<CodeInstruction> instructions, List<Occurence> occurences)
        => Apply(instructions.ToList(), occurences);

    public static List<CodeInstruction> Apply(List<CodeInstruction> instructions, List<Occurence> occurences)
    {
        if (occurences.Count == 0) return instructions.ToList();
        
        var occIdx = 0;

        var result = new List<CodeInstruction>();

        for (int idxInBase = 0; idxInBase < instructions.Count; idxInBase++)
        {
            var occurence = occurences[occIdx];
            var baseInstruction = instructions[idxInBase];
            var idxInPattern = idxInBase - occurence.StartIndex;

            if (idxInPattern < 0)
            {
                if (occurence.Pattern.ShouldTrimBefore)
                {
                    occurence.Pattern.CheckCanRemove(baseInstruction);
                }
                else if (occIdx > 0 && occurences[occIdx - 1].Pattern.ShouldTrimAfter)
                {
                    occurences[occIdx - 1].Pattern.CheckCanRemove(baseInstruction);
                }
                else
                {
                    result.Add(baseInstruction);
                }
            }
            else if (idxInPattern >= occurence.Pattern.Elements.Count)
            {
                if (occurence.Pattern.ShouldTrimAfter)
                {
                    occurence.Pattern.CheckCanRemove(baseInstruction);
                }
                else
                {
                    result.Add(baseInstruction);
                }
            }
            else
            {
                var element = occurence.Pattern.Elements[idxInPattern];

                if (idxInPattern == 0 && occurence.Pattern.InsertBefore != null)
                {
                    foreach (var instruction in occurence.Pattern.InsertBefore)
                    {
                        result.Add(instruction.Clone());
                    }
                }

                if (element.Action != null)
                {
                    var newInstruction = element.Action(baseInstruction);
                    if (newInstruction != null) result.Add(newInstruction);
                }
                else
                {
                    result.Add(baseInstruction);
                }

                if (element.InsertAfter != null)
                {
                    foreach (var instruction in element.InsertAfter)
                    {
                        result.Add(instruction.Clone());
                    }
                }

                if (idxInPattern >= occurence.Pattern.Elements.Count - 1)
                {
                    if (occIdx < occurences.Count - 1) occIdx++;
                    Logger.Log($"Applied transpiler pattern {occurence}");
                }
            }
        }

        return result;
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

    public Element MatchConst(long val) => Match(ci => ci.LoadsConstant(val));

    public Element MatchConst(double val) => Match(ci => ci.LoadsConstant(val));

    public Element MatchConst(string str) => Match(ci => ci.LoadsConstant(str));

    public Element MatchLdarg(int? n = null) => Match(ci => ci.IsLdarg(n));

    public Element MatchStarg(int? n = null) => Match(ci => ci.IsStarg(n));

    public Element MatchLdloc(LocalBuilder local = null) => Match(ci => ci.IsLdloc(local));

    public Element MatchStloc(LocalBuilder local = null) => Match(ci => ci.IsStloc(local));

    public Element MatchCall(Type type, string methodName, Type[] parameters = null, Type[] generics = null)
    {
        var methodInfo = AccessTools.Method(type, methodName, parameters, generics);
        if (methodInfo == null) throw new Exception($"Method {type.FullName}.{methodName} not found");
        return Match(ci => ci.Calls(methodInfo));
    }

    public Element MatchLoad(Type type, string fieldName)
    {
        var fieldInfo = AccessTools.Field(type, fieldName);
        if (fieldInfo == null) throw new Exception($"Field {type.FullName}.{fieldName} not found");
        return Match(ci => ci.LoadsField(fieldInfo));
    }

    public Element MatchStore(Type type, string fieldName)
    {
        var fieldInfo = AccessTools.Field(type, fieldName);
        if (fieldInfo == null) throw new Exception($"Field {type.FullName}.{fieldName} not found");
        return Match(ci => ci.StoresField(fieldInfo));
    }

    public Element MatchNewobj(Type type, Type[] parameters = null)
    {
        var constructorInfo = AccessTools.Constructor(type, parameters);
        if (constructorInfo == null) throw new Exception($"Constructor for {type.FullName} not found");
        return Match(ci => ci.opcode == OpCodes.Newobj && Equals(ci.operand, constructorInfo));
    }

    public TranspilerPattern Insert(OpCode opcode, object operand = null) => Insert(new CodeInstruction(opcode, operand));

    public TranspilerPattern Insert(CodeInstruction instruction)
    {
        if (Elements.Count > 0)
        {
            var element = Elements.Last();
            element.InsertAfter ??= new();
            element.InsertAfter.Add(instruction);
        }
        else
        {
            InsertBefore ??= new();
            InsertBefore.Add(instruction);
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

    public TranspilerPattern Greedy(int minOccurences = 1, int maxOccurences = int.MaxValue)
    {
        IsGreedy = true;
        MinOccurences = minOccurences;
        MaxOccurences = maxOccurences;
        return this;
    }

    public TranspilerPattern Lazy(int occurences = 1)
    {
        IsGreedy = false;
        MinOccurences = occurences;
        MaxOccurences = occurences;
        return this;
    }

    public TranspilerPattern OnlyMatchBefore(TranspilerPattern other)
        => OnlyMatchAfter(other, 0, 0);

    public TranspilerPattern OnlyMatchAfter(TranspilerPattern other, int minCount = 1, int maxCount = int.MaxValue)
    {
        OnlyAfter ??= new();
        OnlyAfter.RemoveAll(e => e.Pattern == other);
        OnlyAfter.Add(new RelativeMatchCondition(other, minCount, maxCount));
        return this;
    }

    protected void CheckCanRemove(CodeInstruction ci)
    {
        if (ci.labels.Count > 0 || ci.blocks.Count > 0)
        {
            Logger.Warn($"Removed instruction {ci} had labels or blocks assigned!");
        }
    }

    protected static string CodePos(int idx) => $"{idx:X4}";

    public class Element
    {
        public readonly TranspilerPattern Pattern;

        protected internal Predicate<CodeInstruction> Matcher;
        protected internal Func<CodeInstruction, CodeInstruction> Action;
        protected internal List<CodeInstruction> InsertAfter;

        protected internal Element(TranspilerPattern pattern, Predicate<CodeInstruction> matcher = null)
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
                if (LdlocToStloc.ContainsKey(ci.opcode))
                {
                    if (StlocToLdloc.ContainsKey(dci.opcode)) dci.opcode = LdlocToStloc[ci.opcode];
                    else if (LdlocToStloc.ContainsKey(dci.opcode)) dci.opcode = ci.opcode;
                }
                else if (StlocToLdloc.ContainsKey(ci.opcode))
                {
                    if (LdlocToStloc.ContainsKey(dci.opcode)) dci.opcode = StlocToLdloc[ci.opcode];
                    else if (StlocToLdloc.ContainsKey(dci.opcode)) dci.opcode = ci.opcode;
                }

                dci.operand = ci.operand;
                Logger.Log($"Stored operand from instruction {ci} to {dci}");
            }
        });
    }

    public readonly struct Occurence
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

        public Occurence(TranspilerPattern pattern, int startIndex)
        {
            Pattern = pattern;
            StartIndex = startIndex;
        }

        public override string ToString()
        {
            return $"{Pattern.Name}@[{CodePos(StartIndex)}-{CodePos(EndIndex)}]";
        }
    }

    protected internal readonly struct RelativeMatchCondition
    {
        public readonly TranspilerPattern Pattern;
        public readonly int MinCount;
        public readonly int MaxCount;

        public bool InRange(int count) => count >= MinCount && count <= MaxCount;

        public RelativeMatchCondition(TranspilerPattern pattern, int minCount, int maxCount)
        {
            Pattern = pattern;
            MinCount = minCount;
            MaxCount = maxCount;
        }
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
