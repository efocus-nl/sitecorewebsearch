using System;
using System.IO;
using Efocus.Sitecore.LuceneWebSearch.Helpers;
using Sitecore.Configuration;
using Sitecore.IO;
using Sitecore.Pipelines;

namespace Efocus.Sitecore.LuceneWebSearch.Pipelines
{
    public class RestoreIndexesResolver
    {
        public void Process(PipelineArgs args)
        {
            string indexesFolder = Settings.GetSetting("IndexFolder", FileUtil.MakePath(Settings.DataFolder, "/indexes"));
            DirectoryInfo indexesDirectoryInfo = new DirectoryInfo(indexesFolder);
            DirectoryInfo[] indexes = indexesDirectoryInfo.GetDirectories();
            foreach (DirectoryInfo index in indexes)
            {
                if (index.Name.EndsWith(".backup"))
                {
                    DirectoryHelper.RestoreDirectoryBackup(
                        index.FullName.Split(new[] {".backup"}, StringSplitOptions.None)[0]);
                    DirectoryHelper.DeleteDirectory(index.FullName);
                }
            }
        }
    }
}
