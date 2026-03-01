using System.Xml;
using System.Xml.Linq;

namespace SoundTouchMCP.Services;

public static class SecureXmlParser
{
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null
    };

    public static XDocument Parse(string xml)
    {
        using var stringReader = new StringReader(xml);
        using var reader = XmlReader.Create(stringReader, ReaderSettings);
        return XDocument.Load(reader);
    }
}