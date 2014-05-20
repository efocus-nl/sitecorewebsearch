using System;
using System.IO;
using BoC.Logging;

namespace Efocus.Sitecore.LuceneWebSearch.Helpers
{
    public class DirectoryHelper
    {
        private ILogger _logger;

        public DirectoryHelper(ILogger logger)
        {
            _logger = logger;
        }

        public void DeleteDirectory(string dir)
        {
            if (!Directory.Exists(dir)) return;

            try
            {
            Directory.Delete(dir, true);
            }
            catch (Exception e)
            {
                _logger.InfoFormat("Backup Manager: Could not delete directory: {0}. Exception: {1}", dir, e);
                return;
            }

            _logger.InfoFormat("Backup Manager: Directory {0} deleted", dir);
        }

        public void DeleteBackupDirectory(string dir)
        {
            _logger.InfoFormat("Backup Manager: Deleting backup directory for: {0}", dir);
            DeleteDirectory(dir + ".backup");
        }

        public void CreateDirectoryBackup(string dir)
        {
            if (String.IsNullOrEmpty(dir))
            {
                _logger.InfoFormat("Backup Manager: Cannot get the path of the current lucene directory: {0}", dir);
            }
            else if (!Directory.Exists(dir))
            {
                _logger.InfoFormat("Backup Manager: Lucene directory does not exist yet, skipping backup {0}", dir);
            }
            else
            {
                var backup = dir + ".backup";
                if (Directory.Exists(backup))
                {
                    _logger.WarnFormat("Backup Manager: Lucene directory backup already exists!! {0} -> We're going to delete that now", backup);
                    try
                    {
                        Directory.Delete(dir, true);
                }
                    catch (Exception e)
                    {
                        //TODO: What should we do now that the backup dir is corrupted?
                        _logger.InfoFormat("Backup Manager: Could not delete directory: {0}. Exception: {1}", backup, e);
                    }
                }
                Directory.CreateDirectory(backup);
                CopyDirectory(dir, backup, true);
                _logger.InfoFormat("Backup Manager: Backup created for: {0}", dir);
            }
        }

        public bool RestoreDirectoryBackup(string dir)
        {
            string backup = dir + ".backup";
            if (Directory.Exists(dir))
            {
                _logger.WarnFormat("Backup Manager: Lucene directory already exists!! {0} -> We're going to delete that now", dir);

                try
                {
                Directory.Delete(dir, true);
                }
                catch (Exception e)
                {
                    _logger.InfoFormat("Backup Manager: Could not delete directory: {0}. Exception: {1}", dir, e);
                    return false;
                }

                if (Directory.Exists(dir))
                {
                    _logger.InfoFormat("Backup Manager: Could not delete directory: {0}", dir);
                    return false;
                }
            }
            Directory.CreateDirectory(dir);
            CopyDirectory(backup, dir, true);
            _logger.InfoFormat("Backup Manager: Backup restored for: {0}", dir);
            return true;
        }

        public void CopyDirectory(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException("Backup Manager: Source directory does not exist or could not be found: " + sourceDirName);
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