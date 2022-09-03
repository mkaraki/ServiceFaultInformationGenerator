using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using X.Web.Sitemap;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ServiceFaultInformationGenerator
{
    static class Program
    {

        private static IDeserializer yamlDeserializer = new DeserializerBuilder()
                    .Build();

        private static string sitemapBase = @"<?xml version=”1.0″ encoding=”UTF-8″?>
<urlset xmlns=”http://www.sitemaps.org/schemas/sitemap/0.9″>
<!-- MAP -->
</urlset>";

        private static readonly string AppDir = AppDomain.CurrentDomain.BaseDirectory;

        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("This application takes 1 or more args.");
                Environment.Exit(1);
            }

            switch (args[0])
            {
                case "build":
                    BuildSite();
                    break;

                case "init":
                    if (args.Length != 2)
                    {
                        Console.Error.WriteLine("No args found");
                        Environment.Exit(1);
                        break;
                    }
                    if (!Directory.Exists(Directory.GetParent(args[1]).FullName))
                    {
                        Console.Error.WriteLine("No directory found");
                        Environment.Exit(6);
                        break;
                    }
                    if (File.Exists(args[1]) || Directory.Exists(args[1]))
                    { 
                        Console.Error.WriteLine("Directory already found");
                        Environment.Exit(7);
                        break;
                    }
                    {
                        Directory.CreateDirectory(args[1]);
                        Directory.CreateDirectory(Path.Combine(args[1], "reports"));
                        CopyFromTemplateDir(args[1], "siteconfig.yaml");
                        CopyFromTemplateDir(args[1], "template.html");
                        CopyFromTemplateDir(args[1], "template.index.html");
                    }
                    break;

                default:
                    Console.Error.WriteLine("No command found");
                    Environment.Exit(2);
                    break;
            }
        }

        private static void CopyFromTemplateDir(string destdir, string name)
        { 
            File.Copy(Path.Combine(AppDir, "template",  name), Path.Combine(destdir, name));
        }

        public static void BuildSite()
        {
            string? sconfpath = FindSiteConfig();
            if (sconfpath == null)
            {
                Console.Error.WriteLine("No site configuration file exist");
                Environment.Exit(3);
                return;
            }

            SiteConfig sconf;
            using (var fs = new StreamReader(sconfpath, System.Text.Encoding.UTF8))
            {
                sconf = yamlDeserializer.Deserialize<SiteConfig>(fs);
            }

            string projdir = Directory.GetParent(sconfpath).FullName;

            string srcdir = Path.Combine(projdir, "reports");
            string outdir = Path.Combine(projdir, "out");

            if (Directory.Exists(outdir))
                Directory.Delete(outdir, true);

            Directory.CreateDirectory(outdir);

            if (!Directory.Exists(srcdir))
            {
                Console.Error.WriteLine("No reports directory exists");
                Environment.Exit(4);
            }

            string templatehtmlpath = Path.Combine(projdir, "template.html");
            if (!File.Exists(templatehtmlpath))
            {
                Console.Error.WriteLine("No template.html exists");
                Environment.Exit(5);
            }

            string templatehtml = File.ReadAllText(templatehtmlpath);

            List<(ReportMetadata, string)> genfiles = new();

            foreach (var f in Directory.GetFiles(srcdir, "*.*", SearchOption.AllDirectories))
            { 
                if (!f.EndsWith(".txt") && !f.EndsWith(".md"))
                    continue;

                string relation = f.Substring(Path.Combine(srcdir, "a").TrimEnd('a').Length);

                relation = string.Join('.', relation.Split('.').Reverse().Skip(1).Reverse());

                var meta = GeneratePage(projdir, f, relation, templatehtml);

                genfiles.Add((meta, relation));
            }

            GenerateIndex(projdir, genfiles.ToArray(), sconf);
        }

        public static ReportMetadata GeneratePage(string projdir, string srcfile, string outplace, string templatehtml)
        {
            string actoutpath = Path.Combine(projdir, "out", outplace);
            Directory.CreateDirectory(Directory.GetParent(actoutpath).FullName);

            string mdcontent = File.ReadAllText(srcfile, System.Text.Encoding.UTF8);

            mdcontent = mdcontent.Replace("\r\n", "\n");

            MarkdownPipeline pipe = new MarkdownPipelineBuilder()
                .UseYamlFrontMatter()
                .UseCustomContainers()
                .UsePipeTables()
                .Build();
            MarkdownDocument document = Markdown.Parse(mdcontent, pipe);
            string mdhtml = document.ToHtml(pipe);

            ReportMetadata meta;


            using (var input = new StringReader(mdcontent))
            {
                var parser = new Parser(input);
                parser.Consume<StreamStart>();
                parser.Consume<DocumentStart>();
                meta = yamlDeserializer.Deserialize<ReportMetadata>(parser);
                parser.Consume<DocumentEnd>();
            }

            string html = templatehtml
                .Replace("<!-- DOC:title -->", meta.Title)
                .Replace("<!-- DOC:content -->", mdhtml)
                .Replace("<!-- DOC:date -->", (meta.UpdateDate ?? meta.Date).ToShortDateString())
                ;

            File.WriteAllText(actoutpath + ".html", html, System.Text.Encoding.UTF8);

            return meta;
        }

        private static void GenerateIndex(string projdir, (ReportMetadata, string)[] files, SiteConfig sconf)
        {
            Sitemap sitemaps = new();

            List<string> indexhtmls = new();

            foreach (var i in files.OrderByDescending(v => v.Item1.UpdateDate ?? v.Item1.Date))
            {
                string webrel = i.Item2
                    .Replace('\\', '/')
                    + ".html";

                sitemaps.Add(new Url() { 
                    Location = $"{sconf.BaseUrl}{webrel}",
                    LastMod = $"{i.Item1.UpdateDate ?? i.Item1.Date:yyyy-MM-dd}"
                });

                indexhtmls.Add($"<tr><td>{i.Item1.UpdateDate ?? i.Item1.Date:yyyy-MM-dd}</td><td><a href=\"{i.Item2 + ".html"}\">{i.Item1.Title}</a></td></tr>");
            }
            var sitemappath = Path.Combine(projdir, "out", "sitemap");
            Directory.CreateDirectory(sitemappath);
            sitemaps.SaveToDirectory(sitemappath);

            var indexhtml = File.ReadAllText(Path.Combine(projdir, "template.index.html"), System.Text.Encoding.UTF8)
                .Replace("<!-- DOC:index -->", String.Join("", indexhtmls));

            File.WriteAllText(Path.Combine(projdir, "out", "index.html"), indexhtml, System.Text.Encoding.UTF8);
        }

        public static string? FindSiteConfig(string? basedir = null)
        {
            bool bbdirnull = basedir == null;
            if (basedir == null)
                basedir = Environment.CurrentDirectory;

            if (File.Exists(Path.Combine(basedir, "siteconfig.yml")))
                return Path.Combine(basedir, "siteconfig.yml");
            else if (File.Exists(Path.Combine(basedir, "siteconfig.yaml")))
                return Path.Combine(basedir, "siteconfig.yaml");
            else if (bbdirnull)
                return FindSiteConfig(Path.Combine(basedir, ".."));
            else
                return null;
        }
    }
}