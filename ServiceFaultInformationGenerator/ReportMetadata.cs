using YamlDotNet.Serialization;

namespace ServiceFaultInformationGenerator
{
    internal class ReportMetadata
    {
        [YamlMember(Alias = "title")]
        public string Title { get; set; }

        [YamlMember(Alias = "date")]
        public DateTime Date { get; set; }

        [YamlMember(Alias = "updateDate")]
        public DateTime? UpdateDate { get; set; } = null;
    }
}
