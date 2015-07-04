using System.IO;
public class FilePatternResolver
{
    public static string[] resolve(string filepattern)
    {
        string filenamepattern = Path.GetFileName(filepattern);
        string filedirectory = filepattern.Substring(0, filepattern.Length - filenamepattern.Length);
        string fullpath = Path.GetFullPath(Path.Combine(".", filedirectory));
        return Directory.GetFiles(fullpath, filenamepattern, SearchOption.TopDirectoryOnly);
    }
}