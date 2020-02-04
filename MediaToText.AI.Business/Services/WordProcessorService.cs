using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

//using System.Data.

namespace MediaToText.AI.Business.Services
{
    public interface IWordProcessorService
    {
        byte[] CreateDocument(List<string> contents);
    }
    public class MicrisoftWordProcessorService: IWordProcessorService
    {
        public byte[] CreateDocument(List<string> contents)
        {
            using (MemoryStream mem = new MemoryStream())
            {
                using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(mem, WordprocessingDocumentType.Document, true))
                {
                    wordDoc.AddMainDocumentPart();
                    // siga a ordem
                    Document doc = new Document();
                    Body body = new Body();

                    contents.ForEach(content =>
                    {
                        Paragraph para = new Paragraph();
                        Run run = new Run();
                        RunProperties runProperties = new RunProperties();
                        Text text = new Text() { Text = content };
                        run.Append(runProperties);
                        run.Append(text);
                        para.Append(run);

                        body.Append(para);
                    });
                    doc.Append(body);
                    wordDoc.MainDocumentPart.Document = doc;
                    wordDoc.Close();
                }
                return mem.ToArray();
            }
        }
    }
}
