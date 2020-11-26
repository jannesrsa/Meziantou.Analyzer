﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Meziantou.Analyzer.Configurations
{
    internal static class EditorConfigFileParser
    {
        // Matches EditorConfig property such as "indent_style = space", see http://editorconfig.org for details
        private static readonly Regex s_propertyMatcher = new(@"^\s*(?<key>[\w\.\-_]+)\s*[=:]\s*(?<value>.*?)\s*([#;].*)?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(2));

        private static readonly StringComparer s_keyComparer = CaseInsensitiveComparison.Comparer;

        /// <summary>
        /// A set of keys that are reserved for special interpretation for the editorconfig specification.
        /// All values corresponding to reserved keys in a (key,value) property pair are always lowercased
        /// during parsing.
        /// </summary>
        /// <remarks>
        /// This list was retrieved from https://github.com/editorconfig/editorconfig/wiki/EditorConfig-Properties
        /// at 2018-04-21 19:37:05Z. New keys may be added to this list in newer versions, but old ones will
        /// not be removed.
        /// </remarks>
        private static readonly ImmutableHashSet<string> s_reservedKeys
            = ImmutableHashSet.CreateRange(s_keyComparer, new[] {
                "root",
                "indent_style",
                "indent_size",
                "tab_width",
                "end_of_line",
                "charset",
                "trim_trailing_whitespace",
                "insert_final_newline",
            });

        /// <summary>
        /// A set of values that are reserved for special use for the editorconfig specification
        /// and will always be lower-cased by the parser.
        /// </summary>
        private static readonly ImmutableHashSet<string> s_reservedValues
            = ImmutableHashSet.CreateRange(s_keyComparer, new[] { "unset" });

        public static EditorConfigFile Load(string filePath)
        {
            if (File.Exists(filePath))
            {
                var lines = File.ReadAllLines(filePath);
                return Parse(lines);
            }

            return EditorConfigFile.Empty;
        }

        public static EditorConfigFile Parse(SourceText text)
        {
            var parsedOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            return Parse(text.Lines.Select(line => line.ToString()));
        }

        private static EditorConfigFile Parse(IEnumerable<string> lines)
        {
            var parsedOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || IsComment(line))
                {
                    continue;
                }

                var propMatches = s_propertyMatcher.Matches(line);
                if (propMatches.Count > 0 && propMatches[0].Success)
                {
                    var key = propMatches[0].Groups["key"].Value;
                    var value = propMatches[0].Groups["value"].Value;

                    Debug.Assert(!string.IsNullOrEmpty(key));
                    Debug.Assert(string.Equals(key, key.Trim(), StringComparison.Ordinal));
                    Debug.Assert(string.Equals(value, value.Trim(), StringComparison.Ordinal));

                    key = CaseInsensitiveComparison.ToLower(key);
                    if (s_reservedKeys.Contains(key) || s_reservedValues.Contains(value))
                    {
                        value = CaseInsensitiveComparison.ToLower(value);
                    }

                    parsedOptions[key] = value ?? "";
                    continue;
                }
            }

            return new EditorConfigFile(parsedOptions);
        }

        private static bool IsComment(string line)
        {
            foreach (char c in line)
            {
                if (!char.IsWhiteSpace(c))
                {
                    return c == '#' || c == ';';
                }
            }

            return false;
        }
    }
}
