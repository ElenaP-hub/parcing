using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Xml.Linq;
using DocumentFormat.OpenXml.EMMA;
using HtmlAgilityPack;
using Milandr.Common;
using Milandr.Enterprise.Core;
using Milandr.Enterprise.Web.Models.NewsRecords;
using NewsRecord = Milandr.Enterprise.Core.NewsRecord;

namespace Milandr.Enterprise.Web.Controllers
{
    public partial class NewsRecordsController : ControllerBase
    {
        [HttpGet]
        public ActionResult DownloadNewsRecords()
        {
            HtmlDocument news = new HtmlWeb().Load("http://insite/news/?SHOWALL_3=1&SIZEN_3=20");
            HtmlDocument events = new HtmlWeb().Load("http://insite/ads/");

            HtmlNodeCollection collectionNews = news.DocumentNode.SelectNodes("//td[contains(@class, 'ads_desc')]/a");
            HtmlNodeCollection collectionEvents = events.DocumentNode.SelectNodes("//td[contains(@class, 'ads_desc')]/a");

            int newsCount = 0;
            StringBuilder stringBuilder = new();

            List<NewsRecord> newsRecords = context.NewsRecords.Where(n => n.Published.HasValue).OrderByDescending(n => n.Published).ToList();

            foreach (var item in collectionNews.Union(collectionEvents))
            {
                var sourceUrl = item.GetAttributeValue("href", null);

                if (!newsRecords.Select(n => n.SourceUrl).Contains(sourceUrl))
                {
                    newsCount++;
                    var div = item.PreviousSibling.PreviousSibling;
                    var published = div.InnerText.ToLower().Trim();
                    var shortHtml = item.NextSibling.NextSibling.InnerText;
                    var defaultSubject = context.NewsRecordSubjects.Where(s => s.Id == Properties.Settings.Default.DefaultNewsRecordSubjectId).FirstOrDefault().ToString();
                    Create record = new()
                    {
                        Id = Guid.NewGuid(),
                        Name = string.IsNullOrWhiteSpace(item.InnerText.Clear()) ? defaultSubject : item.InnerText.Clear(),
                        NewsRecordSubjectId = Properties.Settings.Default.DefaultNewsRecordSubjectId,
                        ShortHtml = string.IsNullOrWhiteSpace(shortHtml) ? defaultSubject : shortHtml,
                        SourceUrl = sourceUrl
                    };

                    var small = div.ParentNode.PreviousSibling.PreviousSibling;
                    var smallUrl = small.FirstChild.NextSibling.GetAttributeValue("style", "").ToString();
                    smallUrl = smallUrl.Substring(smallUrl.IndexOf('/'), smallUrl.Length - (1 + smallUrl.IndexOf('/')));

                    DateTime.TryParseExact(published, "dd MMMM yyyy года", CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.NoCurrentDateDefault, out DateTime date);
                    record.Published = date.AddHours(DateTime.Now.Hour).AddMinutes(DateTime.Now.Minute).AddSeconds(DateTime.Now.Second);

                    HtmlDocument newsRec = new HtmlWeb().Load(string.Concat("http://insite", record.SourceUrl));

                    if (newsRec.DocumentNode.SelectSingleNode("//div[contains(@class, 'ndc_desc')]") != null)
                    {
                        record.Html = newsRec.DocumentNode.SelectSingleNode("//div[contains(@class, 'ndc_desc')]").InnerHtml;
                    }
                    else
                    {
                        record.Html = newsRec.DocumentNode.SelectSingleNode("//div[contains(@id, 'ads_detail')]").InnerHtml;
                    }
                    record.Tags = new();

                    using (WebClient client = new())
                    {
                        record.SmallImageBytes = client.DownloadData($"http://insite{smallUrl}");
                        var contentType = client.ResponseHeaders["Content-Type"];
                        try
                        {
                            CreateImage(record.SmallImageBytes, record.Id, NewsRecordImageType.Small, contentType);
                        }
                        catch (DbEntityValidationException ex)
                        {
                            ex.EntityValidationErrors.SelectMany(ve => ve.ValidationErrors).ToList().ForEach(vr => stringBuilder.AppendLine(vr.PropertyName + vr.ErrorMessage));
                        }
                    }

                    if (newsRec.DocumentNode.SelectSingleNode("//div[@class='ndc_img']/a") != null)
                    {
                        var banner = newsRec.DocumentNode.SelectSingleNode("//div[contains(@class, 'ndc_img')]/a");
                        var bannerUrl = banner.GetAttributeValue("data-clipboard-text", "");

                        using (WebClient client = new())
                        {
                            record.MainImageBytes = client.DownloadData(bannerUrl);

                            var contentType = client.ResponseHeaders["Content-Type"];
                            try
                            {
                                CreateImage(record.MainImageBytes, record.Id, NewsRecordImageType.Banner, contentType);

                            }
                            catch (DbEntityValidationException ex)
                            {
                                ex.EntityValidationErrors.SelectMany(ve => ve.ValidationErrors).ToList().ForEach(vr => stringBuilder.AppendLine(vr.PropertyName + vr.ErrorMessage));
                            }
                        }
                    }
                    try
                    {
                        var created = context.NewsRecordsService.Create(record.Id, null, record.Name, CurrentUserId, record.Html, record.Published, record.ShortHtml, DateTime.Now, null, record.NewsRecordSubjectId, null,
                        false, null, record.SourceUrl, record.Tags);

                        context.ValidationContextItems.Add(created, NewsRecord.SkipPublishedCreatedValidation);
                        stringBuilder.AppendLine($"Add {record.Name} <br>");

                        context.SaveChanges();
                    }
                    catch (DbEntityValidationException ex)
                    {
                        ex.EntityValidationErrors.SelectMany(ve => ve.ValidationErrors).ToList().ForEach(vr => stringBuilder.AppendLine(vr.PropertyName + vr.ErrorMessage));
                    }
                }
            }
            _ = newsCount == 0 ? stringBuilder.AppendLine($"No news to download") : stringBuilder.AppendLine($"Add {newsCount} news");

            return Content(stringBuilder.ToString());
        }
    }
}