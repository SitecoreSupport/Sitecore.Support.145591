using Sitecore.Configuration;
using Sitecore.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Sitecore.Support.Data.Serialization
{
  public static class PathUtils
  {
    private static readonly MD5 _md5;
    private static readonly Func<char, string> EncodingAlgorithm;
    private static readonly char[] IllegalCharacters;
    private static readonly List<KeyValuePair<char, string>> IllegalSymbolsToReplace;

    static PathUtils()
    {
      _md5 = MD5.Create();
      IllegalCharacters = new char[] { '%', '$' };
      EncodingAlgorithm = delegate (char inp)
      {
        char ch = '%';
        int num = inp;
        return ch.ToString() + num.ToString("X2");
      };
      IllegalSymbolsToReplace = (from ch in IllegalCharacters.Concat<char>(Path.GetInvalidFileNameChars()).Concat<char>(Settings.Serialization.InvalidFileNameChars).Distinct<char>().Except<char>(new char[] { Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar }) select new KeyValuePair<char, string>(ch, EncodingAlgorithm(ch))).ToList<KeyValuePair<char, string>>();
    }

    public static string GetDirectoryPath(string itempath, string root)
    {
      return MapItemPath(itempath, root);
    }

    private static string MapItemPath(string itemPath, string root)
    {
      Assert.IsFalse(Path.IsPathRooted(itemPath), "itemPath is rooted");
      itemPath = ReplaceIllegalCharsByConfig(itemPath);
      int num = 240 - Settings.Serialization.SerializationFolderPathMaxLength;
      int startIndex = (itemPath.Length / num) * num;
      if (startIndex == 0)
      {
        return Path.Combine(root, itemPath).Replace('/', Path.DirectorySeparatorChar);
      }
      int index = itemPath.IndexOf('/', startIndex);
      if (index < 0)
      {
        index = itemPath.LastIndexOf('/', startIndex - 1);
      }
      if (index <= 0)
      {
        return Path.Combine(root, itemPath);
      }
      string contents = itemPath.Substring(0, index).Replace('/', Path.DirectorySeparatorChar);
      string str2 = itemPath.Substring(index + 1).Replace('/', Path.DirectorySeparatorChar);
      if (contents.Length <= 0)
      {
        return Path.Combine(root, str2);
      }
      //used fixed method to provide custom root path
      string shortPath = GetShortPath(root + contents, root);
      //end fix method call
      if (!File.Exists(Path.Combine(shortPath, "link")))
      {
        Directory.CreateDirectory(shortPath);
        File.WriteAllText(Path.Combine(shortPath, "link"), contents);
      }
      return Path.Combine(shortPath, str2);
    }

    internal static string ReplaceIllegalCharsByConfig(string path)
    {
      Assert.ArgumentNotNull(path, "path");
      return HandleIllegalSymbols(path, pair => pair.Key.ToString(), pair => pair.Value);
    }

    private static string HandleIllegalSymbols(string str, Func<KeyValuePair<char, string>, string> keySelector, Func<KeyValuePair<char, string>, string> valueSelector)
    {
      return IllegalSymbolsToReplace.Aggregate<KeyValuePair<char, string>, StringBuilder>(new StringBuilder(str), (current, pair) => current.Replace(keySelector(pair), valueSelector(pair))).ToString();
    }

    public static string GetShortPath(string path, string root)
    {
      //fix applied to compare with the given root path instead of path defined in config
      if (!path.StartsWith(root, StringComparison.InvariantCultureIgnoreCase))
      {
        throw new Exception("path is not under the root");
      }
      return Path.Combine(root, GetHash(path.Substring(root.Length)));
      //end fix
    }

    private static string GetHash(string s)
    {
      byte[] bytes = Encoding.UTF8.GetBytes(s.ToLowerInvariant());
      _md5.Initialize();
      bytes = _md5.ComputeHash(bytes);
      for (int i = 0; i < bytes.Length; i++)
      {
        bytes[i % 4] = (byte)(bytes[i % 4] ^ bytes[i]);
      }
      StringBuilder builder = new StringBuilder();
      for (int j = 0; j < 4; j++)
      {
        builder.Append(bytes[j].ToString("X" + 2));
      }
      return builder.ToString();
    }

  }
}