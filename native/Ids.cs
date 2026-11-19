using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace AlienBridge {
  // IDs persist with the agent save. Only encountered glyphs receive an ID.
  static class Ids {
    static readonly string directory = Path.GetDirectoryName(typeof(Hooks).Assembly.Location);
    static readonly string[] alphabet = File.ReadAllLines(Path.Combine(directory, "ids.txt"));
    static readonly string path = Path.Combine(Hooks.SaveDir(), "glyph-ids.json");
    static readonly Dictionary<string, string> words = File.Exists(path)
      ? JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path))
      : new Dictionary<string, string>();
    static readonly Dictionary<string, string> codes = words.ToDictionary(pair => pair.Value, pair => pair.Key);
    static readonly Dictionary<string, string> unicode = JsonConvert.DeserializeObject<Dictionary<string, string>>(
      File.ReadAllText(Path.Combine(directory, "unicode.json")));

    static readonly Queue<string> available = new Queue<string>(alphabet
      .Where(code => !codes.ContainsKey(code)).OrderBy(_ => Guid.NewGuid()));

    public static string Of(string word) {
      if (string.IsNullOrEmpty(word))
        return null;
      if (words.TryGetValue(word, out var code))
        return code;
      if (available.Count == 0)
        throw new InvalidOperationException("Glyph ID alphabet exhausted");
      code = available.Dequeue();
      words.Add(word, code);
      codes.Add(code, word);
      File.WriteAllText(path, JsonConvert.SerializeObject(words));
      return code;
    }

    public static string Word(string code) => codes.TryGetValue(code, out var word)
      ? word : throw new ArgumentException("Unknown glyph ID: " + code);

    public static string Unicode(string word) => unicode.TryGetValue(word, out var glyph) ? glyph : null;
    public static IEnumerable<KeyValuePair<string, string>> Discovered => codes;
  }
}
