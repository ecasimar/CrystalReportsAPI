using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;

namespace CrystalReportsAPI.Controllers
{
    [RoutePrefix("api/crystal")]
    public class CrystalController : ApiController
    {
        [HttpGet]
        [Route("OrdenDeCompra/{idOrden}")]
        public HttpResponseMessage GenerarReportePdf(int idOrden)
        {
            // Inicializar el reporte fuera del try para poder manejarlo con seguridad
            ReportDocument rptDoc = new ReportDocument();

            try
            {
                string rutaBase = AppDomain.CurrentDomain.BaseDirectory;
                string rutaReporte = Path.Combine(rutaBase, "Reportes", "FCO-03.rpt");

                if (!File.Exists(rutaReporte))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, $"No se encontró la plantilla del reporte en la ruta: {rutaReporte}");
                }

                // 1. Cargar el reporte
                rptDoc.Load(rutaReporte);

                // 2. Definir parámetros de conexión a SAP HANA
                string dbName = "CENTRAL_PRUEBASAP10"; // Asegúrate de que este nombre sea el correcto en tu HANA

                ConnectionInfo hanaConnection = new ConnectionInfo
                {
                    ServerName = "192.168.104.232:30013",
                    DatabaseName = dbName,
                    UserID = "System",
                    Password = "Sapb1234",
                    Type = ConnectionInfoType.SQL
                };

                // 3. Aplicar conexión al reporte principal
                foreach (Table table in rptDoc.Database.Tables)
                {
                    TableLogOnInfo logOnInfo = table.LogOnInfo;
                    logOnInfo.ConnectionInfo = hanaConnection;
                    table.ApplyLogOnInfo(logOnInfo);
                    // QUITAMOS la línea de table.Location
                }

                // 4. Aplicar conexión a todos los SUBREPORTES
                foreach (ReportDocument subReporte in rptDoc.Subreports)
                {
                    foreach (Table table in subReporte.Database.Tables)
                    {
                        TableLogOnInfo logOnInfo = table.LogOnInfo;
                        logOnInfo.ConnectionInfo = hanaConnection;
                        table.ApplyLogOnInfo(logOnInfo);
                        // QUITAMOS la línea de table.Location
                    }
                }

                // 5. Inyectar el parámetro forzándolo a Int64 (BigInt en HANA)
                rptDoc.SetParameterValue("DocKey@", Convert.ToInt64(idOrden));

                // 6. Exportar a Stream de memoria
                Stream pdfStream = rptDoc.ExportToStream(ExportFormatType.PortableDocFormat);

                // 7. Construir respuesta HTTP de descarga de archivos
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StreamContent(pdfStream);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = $"Orden_Compra_{idOrden}.pdf"
                };

                return response;
            }
            catch (Exception ex)
            {
                // Si hay error, nos aseguramos de limpiar el objeto corrupto
                if (rptDoc != null)
                {
                    rptDoc.Close();
                    rptDoc.Dispose();
                }
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, $"Error al procesar Crystal Reports: {ex.Message}");
            }
            // NOTA: Se eliminó el bloque 'finally' de liberación inmediata para no interrumpir el flujo del Stream.
        }
    }
}