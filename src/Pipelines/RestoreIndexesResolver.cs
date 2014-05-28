using System;
using System.IO;
using BoC.InversionOfControl;
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
            if (!indexesDirectoryInfo.Exists) return;

            DirectoryInfo[] indexes = indexesDirectoryInfo.GetDirectories();

            DirectoryHelper directoryHelper = IoC.Resolver.Resolve<DirectoryHelper>();

            foreach (DirectoryInfo index in indexes)
            {
                if (index.Name.EndsWith(".backup"))
                {
                    directoryHelper.RestoreDirectoryBackup(
                        index.FullName.Split(new[] {".backup"}, StringSplitOptions.None)[0]);
                    directoryHelper.DeleteDirectory(index.FullName);
                }
            }
        }
    }
}
