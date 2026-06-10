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
        /// <summary>
        /// Genera un documento PDF a partir de un archivo .rpt de Crystal Reports.
        /// </summary>
        /// <param name="idOrden">ID o número de la orden que requiere el reporte.</param>
        [HttpPost]
        [Route("generar-pdf/{idOrden}")]
        public HttpResponseMessage GenerarReportePdf(int idOrden)
        {
            ReportDocument rptDoc = new ReportDocument();

            try
            {
                // 1. Obtener la ruta física de la carpeta "Reportes" en la raíz del proyecto
                string rutaBase = AppDomain.CurrentDomain.BaseDirectory;
                string rutaReporte = Path.Combine(rutaBase, "Reportes", "ORDCOMP-V4.rpt");

                // Verificar que el archivo .rpt realmente exista en el servidor
                if (!File.Exists(rutaReporte))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, $"No se encontró la plantilla del reporte en la ruta: {rutaReporte}");
                }

                // 2. Cargar el reporte en el motor de Crystal
                rptDoc.Load(rutaReporte);

                // 3. [OPCIONAL] Si tu reporte se conecta directo a SQL Server, descomenta y llena esta línea:
                // rptDoc.SetDatabaseLogon("tu_usuario", "tu_password", "tu_servidor", "tu_base_datos");

                // 4. Inyectar el parámetro que pide tu reporte (Asegúrate de que se llame igual en el .rpt)
                rptDoc.SetParameterValue("IdOrden", idOrden);

                // 5. Exportar el reporte directamente a un flujo de memoria (Stream) en formato PDF
                // Esto evita tener que guardar archivos basura en el disco duro del servidor
                Stream pdfStream = rptDoc.ExportToStream(ExportFormatType.PortableDocFormat);

                // 6. Construir la respuesta HTTP con el archivo binario
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StreamContent(pdfStream);

                // 7. Configurar las cabeceras para que el navegador y Swagger entiendan que es un PDF descargable
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = $"Orden_Compra_{idOrden}.pdf"
                };

                return response;
            }
            catch (Exception ex)
            {
                // Si ocurre un error, devolvemos un estado 500 con el mensaje detallado
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, $"Error al procesar Crystal Reports: {ex.Message}");
            }
            finally
            {
                // 8. REGLA DE ORO: Liberar los objetos COM de Crystal para evitar fugas de memoria en el pool de IIS
                if (rptDoc != null)
                {
                    rptDoc.Close();
                    rptDoc.Dispose();
                    GC.Collect(); // Ayuda a limpiar la memoria de inmediato
                }
            }
        }
    }
}