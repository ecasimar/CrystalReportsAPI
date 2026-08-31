using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Http;

namespace CrystalReportsAPI.Controllers
{
    [RoutePrefix("api/Ventas")]
    public class VentasController : ApiController
    {
        [HttpGet]
        [Route("CCP/{UUID}")]

        public HttpResponseMessage GenerarReporteCCP(string UUID, [FromUri] string empresa = null)
        {
            if (string.IsNullOrEmpty(empresa))
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Falta el parámetro 'empresa' en la URL. Ejemplo: ?empresa=EMPRESA1");
            }
            string codigoEmpresa = empresa.ToUpper();
            string llaveConfig = $"{codigoEmpresa}";
            string dbName = ConfigurationManager.AppSettings[llaveConfig];
            if (string.IsNullOrEmpty(dbName))
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, $"La empresa '{codigoEmpresa}' no está configurada.");
            }
            ReportDocument rptDoc = new ReportDocument();
            try
            {
                string rutaBase = AppDomain.CurrentDomain.BaseDirectory;
                string rutaReporte = Path.Combine(rutaBase, "Reportes", "EntregaCCP.rpt");
                if (!File.Exists(rutaReporte))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, $"No se encontró la plantilla del reporte en la ruta: {rutaReporte}");
                }
                // Cargar el reporte
                rptDoc.Load(rutaReporte);
                ConnectionInfo hanaConnection = new ConnectionInfo
                {
                    ServerName = ConfigurationManager.AppSettings["Hana_Server"],
                    DatabaseName = dbName, // <- Dinámico por empresa
                    UserID = ConfigurationManager.AppSettings["Hana_User"],
                    Password = ConfigurationManager.AppSettings["Hana_Password"],
                    Type = ConnectionInfoType.SQL
                };

                foreach (Table table in rptDoc.Database.Tables)
                {
                    TableLogOnInfo logOnInfo = table.LogOnInfo;
                    logOnInfo.ConnectionInfo = hanaConnection;
                    table.ApplyLogOnInfo(logOnInfo);
                    if (table.Name.ToUpper() != "COMMAND" && table.Name.ToUpper() != "COMANDO")
                    {
                        table.Location = $"{dbName}.{table.Name}";
                    }
                }

                foreach (ReportDocument subReporte in rptDoc.Subreports)
                {
                    foreach (Table table in subReporte.Database.Tables)
                    {
                        TableLogOnInfo logOnInfo = table.LogOnInfo;
                        logOnInfo.ConnectionInfo = hanaConnection;
                        table.ApplyLogOnInfo(logOnInfo);

                        if (table.Name.ToUpper() != "COMMAND" && table.Name.ToUpper() != "COMANDO")
                        {
                            table.Location = $"{dbName}.{table.Name}";
                        }
                    }
                }

                rptDoc.SetParameterValue("UUID@", UUID.ToString());
                rptDoc.SetParameterValue("EsquemaBD", dbName);

                Stream pdfStream = rptDoc.ExportToStream(ExportFormatType.PortableDocFormat);

                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StreamContent(pdfStream);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = $"CCP_{codigoEmpresa}_{UUID}.pdf" // Nombre dinámico incluyendo la empresa
                };

                return response;
            }
            catch (Exception ex)
            {
                if (rptDoc != null)
                {
                    rptDoc.Close();
                    rptDoc.Dispose();
                }
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, $"Error al procesar Crystal Reports para {codigoEmpresa}: {ex.Message}");
            }
        }
    }
}