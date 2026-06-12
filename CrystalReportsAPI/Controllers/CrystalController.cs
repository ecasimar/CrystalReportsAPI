using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Configuration; // Requerido para leer Web.config
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using System.Linq;

namespace CrystalReportsAPI.Controllers
{
    [RoutePrefix("api/crystal")]
    public class CrystalController : ApiController
    {
        [HttpGet]
        [Route("OrdenDeCompra/{idOrden}")]
        public HttpResponseMessage GenerarReportePdf(int idOrden, [FromUri] string empresa = null)
        {
            // Validamos si el usuario olvidó poner el parámetro ?empresa=...
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
                string rutaReporte = Path.Combine(rutaBase, "Reportes", "FCO-03.rpt");

                if (!File.Exists(rutaReporte))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, $"No se encontró la plantilla del reporte en la ruta: {rutaReporte}");
                }

                // Cargar el reporte
                rptDoc.Load(rutaReporte);

                // 3. Recuperar credenciales genéricas del Web.config
                ConnectionInfo hanaConnection = new ConnectionInfo
                {
                    ServerName = ConfigurationManager.AppSettings["Hana_Server"],
                    DatabaseName = dbName, // <- Dinámico por empresa
                    UserID = ConfigurationManager.AppSettings["Hana_User"],
                    Password = ConfigurationManager.AppSettings["Hana_Password"],
                    Type = ConnectionInfoType.SQL
                };

                // 4. Aplicar conexión al reporte principal
                foreach (Table table in rptDoc.Database.Tables)
                {
                    TableLogOnInfo logOnInfo = table.LogOnInfo;
                    logOnInfo.ConnectionInfo = hanaConnection;
                    table.ApplyLogOnInfo(logOnInfo);
                }

                // 5. Aplicar conexión a todos los SUBREPORTES
                foreach (ReportDocument subReporte in rptDoc.Subreports)
                {
                    foreach (Table table in subReporte.Database.Tables)
                    {
                        TableLogOnInfo logOnInfo = table.LogOnInfo;
                        logOnInfo.ConnectionInfo = hanaConnection;
                        table.ApplyLogOnInfo(logOnInfo);
                    }
                }

                // Inyectar parámetro
                rptDoc.SetParameterValue("DocKey@", Convert.ToInt64(idOrden));

                // Exportar a Stream
                Stream pdfStream = rptDoc.ExportToStream(ExportFormatType.PortableDocFormat);

                // Construir respuesta HTTP
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StreamContent(pdfStream);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = $"Orden_Compra_{codigoEmpresa}_{idOrden}.pdf" // Nombre dinámico incluyendo la empresa
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