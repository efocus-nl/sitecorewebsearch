using System;
using System.IO;

namespace Efocus.Sitecore.LuceneWebSearch.Helpers
{
    public class DirectoryHelper
    {
        public static void DeleteDirectory(string dir)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }

        public static void DeleteBackupDirectory(string dir)
        {
            DeleteDirectory(dir + ".backup");
        }

        public static void CreateDirectoryBackup(string dir)
        {
            if (String.IsNullOrEmpty(dir))
            {
                //_logger.InfoFormat("Cannot get the path of the current lucene directory: {0}", _index.Directory);
            }
            else if (!Directory.Exists(dir))
            {
                //_logger.InfoFormat("Websearch: Lucene directory does not exist yet, skipping backup {0}", _index.Directory);
            }
            else
            {
                var backup = dir + ".backup";
                if (Directory.Exists(backup))
                {
                    //_logger.WarnFormat("Websearch: Lucene directory backup already exists!! {0} -> We're going to delete that now", backup);
                    Directory.Delete(backup, true);
                }
                Directory.CreateDirectory(backup);
                CopyDirectory(dir, backup, true);
            }
        }

        public static void RestoreDirectoryBackup(string dir)
        {
            string backup = dir + ".backup";
            if (String.IsNullOrEmpty(backup))
            {
                //_logger.InfoFormat("Cannot get the path of the current lucene backup directory: {0}", _index.Directory);
            }
            else
            {
                if (Directory.Exists(dir))
                {
                    //_logger.WarnFormat("Websearch: Lucene directory already exists!! {0} -> We're going to delete that now", dir);
                    Directory.Delete(dir, true);
                }
                Directory.CreateDirectory(dir);
                CopyDirectory(backup, dir, true);
            }
        }

        public static void CopyDirectory(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
            }

            // If the destination directory doesn't exist, create it. 
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location. 
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    CopyDirectory(subdir.FullName, temppath, copySubDirs);
                }
            }
        }
    }
}