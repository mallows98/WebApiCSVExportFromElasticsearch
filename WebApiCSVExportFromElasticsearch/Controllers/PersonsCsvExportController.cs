﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Web.Http;
using ElasticsearchCRUD;
using ElasticsearchCRUD.ContextSearch;
using Microsoft.AspNet.SignalR;
using WebApiCSVExportFromElasticsearch.Models;

namespace WebApiCSVExportFromElasticsearch.Controllers
{
    public class PersonsCsvExportController : ApiController
    {
		private readonly IHubContext _hubContext = GlobalHost.ConnectionManager.GetHubContext<DiagnosisEventSourceService>();

		[Route("api/PersonsCsvExport")]
		public IHttpActionResult GetPersonsCsvExport()
		{
			_hubContext.Clients.All.addDiagnosisMessage(string.Format("Csv export starting"));

			// force that this method always returns an excel document.
			Request.Headers.Accept.Clear();
			Request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.ms-excel"));

			_hubContext.Clients.All.addDiagnosisMessage(string.Format("ScanAndScrollConfiguration: 1s, 300 items pro shard"));
			_hubContext.Clients.All.addDiagnosisMessage(string.Format("sending scan and scroll _search"));
			_hubContext.Clients.All.addDiagnosisMessage(BuildSearchMatchAll());

			var result = new List<Person>(); 
			using (var context = new ElasticsearchContext("http://localhost:9200/", new ElasticsearchMappingResolver()))
			{
				context.TraceProvider = new SignalRTraceProvider(_hubContext, TraceEventType.Information);

				var scanScrollConfig = new ScanAndScrollConfiguration(1, TimeUnits.Second, 300);
				var scrollIdResult = context.SearchCreateScanAndScroll<Person>(BuildSearchMatchAll(), scanScrollConfig);
				
				var scrollId = scrollIdResult.ScrollId;
				_hubContext.Clients.All.addDiagnosisMessage(string.Format("Total Hits: {0}", scrollIdResult.TotalHits));

				int processedResults = 0;
				while (scrollIdResult.TotalHits > processedResults)
				{
					var resultCollection = context.Search<Person>("", scrollId, scanScrollConfig);
					scrollId = resultCollection.ScrollId;

					result.AddRange(resultCollection.PayloadResult);
					processedResults = result.Count;
					_hubContext.Clients.All.addDiagnosisMessage(string.Format("Total Hits: {0}, Processed: {1}", scrollIdResult.TotalHits, processedResults));
				}
			}

			_hubContext.Clients.All.addDiagnosisMessage(string.Format("Elasticsearch proccessing finished, starting to serialize csv"));
			return Ok(result);
		}

			//{
		//	"query" : {
		//		"match_all" : {}
		//	}
		//}
		private string BuildSearchMatchAll()
		{
			var buildJson = new StringBuilder();
			buildJson.AppendLine("{");
			buildJson.AppendLine("\"query\": {");
			buildJson.AppendLine("\"match_all\" : {}");
			buildJson.AppendLine("}");
			buildJson.AppendLine("}");

			return buildJson.ToString();
		}
    }
}
