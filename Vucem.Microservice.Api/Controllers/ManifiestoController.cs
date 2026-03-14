using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Web.Http;
using Vucem.Microservice.Api.Security; // O el namespace que le hayas puesto
using Vucem.Microservice.Api.VucemIngresoAPI;
using Vucem.Microservice.Api.VucemIngresoAPI; // El namespace de tu Reference.cs

namespace Vucem.Microservice.Api.Controllers
{
    [RoutePrefix("api/vucem")]
    public class ManifiestoController : ApiController
    {
        [HttpPost]
        [Route("enviar")]
        public async Task<IHttpActionResult> EnviarManifiesto([FromBody] PeticionVucemDto peticion)
        {
            try
            {
                if (peticion == null)
                {
                    return BadRequest("El JSON enviado es inválido, está vacío o no tiene el formato correcto.");
                }
                // 1. Apagamos Expect-Continue a nivel aplicación (En .NET 4.8 esto funciona perfecto a nivel global)
                System.Net.ServicePointManager.Expect100Continue = false;
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                // 2. Aquí llamarías a tu clase IVucemSigner para obtener el Base64 de la firma.
                // Como ejemplo asumo que .NET 8 ya te mandó la firma en Base64 en el DTO o la calculas aquí:
                byte[] firmaBytes = Convert.FromBase64String(peticion.FirmaBase64);
                byte[] certBytes = Convert.FromBase64String(peticion.CertificadoBase64);

                // 3. Llenamos el objeto nativo de VUCEM
                var infoManifestacion = new InformacionManifestacion
                {
                    firmaElectronica = new FirmaElectronica
                    {
                        cadenaOriginal = peticion.CadenaOriginal,
                        firma = firmaBytes,
                        certificado = certBytes
                    },
                    importadorexportador = new ImportadorExportador { rfc = peticion.RfcImportador }
                };

                // 4. Creamos el cliente usando la configuración del Web.config
                var client = new IngresoManifestacionRemoteClient("IngresoManifestacionPort1");

                // 5. Inyectamos las credenciales y el Timestamp de seguridad
                client.Endpoint.EndpointBehaviors.Add(new VucemEndpointBehavior(peticion.UsuarioWcf, peticion.PasswordWcf));

                // 6. ¡Enviamos al SAT!
                var respuesta = await client.registroManifestacionAsync(infoManifestacion);

                // 7. Retornamos el objeto JSON limpio a tu API de .NET 8
                return Ok(respuesta);
            }
            catch (FaultException ex)
            {
                return BadRequest($"Error de Negocio VUCEM: {ex.Message}");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    // El DTO que recibiremos como JSON desde .NET 8
    public class PeticionVucemDto
    {
        public string CadenaOriginal { get; set; }
        public string RfcImportador { get; set; }
        public string FirmaBase64 { get; set; }
        public string CertificadoBase64 { get; set; }
        public string UsuarioWcf { get; set; }
        public string PasswordWcf { get; set; }
    }
}