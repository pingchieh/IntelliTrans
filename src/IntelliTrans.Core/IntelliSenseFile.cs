using System.Xml;

namespace IntelliTrans.Core;

/// <summary>
/// 表示一个IntelliSense XML文档文件，提供解析和提取其中内容的功能。
/// </summary>
public class IntelliSenseFile
{
    private XmlDocument doc;

    private IntelliSenseFile(string xmlpath)
    {
        doc = LoadXml(xmlpath);
    }

    /// <summary>
    /// 获取指定标签名称的 XML 元素集合。
    /// </summary>
    /// <param name="tagName">要查找的标签名称。</param>
    /// <returns>包含所有具有指定标签名称的 XML 元素的集合。</returns>
    private IEnumerable<XmlElement> GetXmlElementsByTag(string tagName)
    {
        return doc.GetElementsByTagName(tagName).Cast<XmlElement>();
    }

    /// <summary>
    /// 根据指定的标签名获取所有元素的内部 XML 内容。
    /// </summary>
    /// <param name="tagName">要查找的标签名。</param>
    /// <returns>一个包含所有匹配元素的内部 XML 内容的字符串集合。</returns>
    private IEnumerable<string> GetContentsByTag(string tagName)
    {
        return GetXmlElementsByTag(tagName).Select(x => x.InnerXml);
    }

    /// <summary>
    /// 根据指定的标签名称数组获取内容集合。
    /// </summary>
    /// <param name="tagName">要查找的标签名称数组。</param>
    /// <returns>包含指定标签的所有内容的字符串集合。</returns>
    public IEnumerable<string> GetContentsByTags(params string[] tagName)
    {
        var result = new List<string>();
        foreach (string tag in tagName)
        {
            result.AddRange(GetContentsByTag(tag));
        }
        return result;
    }

    /// <summary>
    /// 获取具有指定标签名称的 XML 元素集合。
    /// </summary>
    /// <param name="tagName">要查找的标签名称数组。</param>
    /// <returns>包含所有匹配标签的 XML 元素集合。</returns>
    public IEnumerable<XmlElement> GetXmlElementsByTags(params string[] tagName)
    {
        var result = new List<XmlElement>();
        foreach (string tag in tagName)
        {
            result.AddRange(GetXmlElementsByTag(tag));
        }
        return result;
    }

    /// <summary>
    /// 解析指定路径的 IntelliSense 文件。
    /// </summary>
    /// <param name="path">要解析的 IntelliSense 文件的路径。</param>
    /// <returns>如果成功解析，则返回 <see cref="IntelliSenseFile"/> 对象；否则，返回 null。</returns>
    public static IntelliSenseFile? Parse(string path)
    {
        var file = new IntelliSenseFile(path);
        return !IsIntellisenseXml(file.doc) ? null : file;
    }

    /// <summary>
    /// 检查给定的 XML 文档是否为智能感知 XML 文档。
    /// </summary>
    /// <param name="doc">要检查的 XML 文档。</param>
    /// <returns>如果文档是智能感知 XML 文档，则返回 true；否则返回 false。</returns>
    private static bool IsIntellisenseXml(XmlDocument doc)
    {
        var docNode = FindXmlNote(doc.ChildNodes, "doc");
        if (docNode == null)
        {
            return false;
        }

        var assemblyNode = FindXmlNote(docNode.ChildNodes, "assembly");
        return assemblyNode != null
            && FindXmlNote(assemblyNode.ChildNodes, "name") != null
            && FindXmlNote(docNode.ChildNodes, "members") != null;
    }

    /// <summary>
    /// 在XML节点列表中查找指定名称的XML节点。
    /// </summary>
    /// <param name="nodes">要搜索的XML节点列表。</param>
    /// <param name="name">要查找的节点的名称。</param>
    /// <returns>如果找到具有指定名称的节点，则返回该节点；否则返回null。</returns>
    private static XmlNode? FindXmlNote(XmlNodeList nodes, string name)
    {
        return nodes.Cast<XmlNode>().FirstOrDefault(item => item.Name == name);
    }

    /// <summary>
    /// 从指定的 XML 文件加载 XML 文档。
    /// </summary>
    /// <param name="xmlFile">要加载的 XML 文件的路径。</param>
    /// <returns>加载的 XmlDocument 对象。</returns>
    private XmlDocument LoadXml(string xmlFile)
    {
        XmlDocument doc = new();
        doc.Load(xmlFile);
        return doc;
    }

    /// <summary>
    /// 将XML文档保存到指定路径
    /// </summary>
    /// <param name="savePath">保存XML文档的完整路径。</param>
    public void SaveXml(string savePath)
    {
        string? dir = Path.GetDirectoryName(savePath);
        if (dir == null)
        {
            return;
        }
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        doc.Save(savePath);
    }

    /// <summary>
    /// 检查给定的字符串是否为有效的 XML 片段。
    /// </summary>
    /// <param name="xml">要验证的 XML 字符串。</param>
    /// <returns>如果 XML 字符串有效则返回 true；如果字符串为空或无效，则返回 false。</returns>
    public static bool IsValidXml(string xml)
    {
        if (string.IsNullOrEmpty(xml))
        {
            return true;
        }

        try
        {
            // 创建一个临时的完整XML文档来验证
            string wrappedXml = $"<root>{xml}</root>";
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(wrappedXml);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
