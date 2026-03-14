using System;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Web.Http;
using Vucem.Microservice.Api.Security;
using Vucem.Microservice.Api.VucemIngresoAPI;
using Vucem.Microservice.Api.VucemDigitalizacionAPI; 
using Vucem.Microservice.Api.Dto;
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
                if (peticion == null) return BadRequest("El JSON enviado es inválido.");

                System.Net.ServicePointManager.Expect100Continue = false;
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                // Usamos el purificador seguro
                byte[] firmaBytes = ConvertirBase64Seguro(peticion.FirmaBase64);
                byte[] certBytes = ConvertirBase64Seguro(peticion.CertificadoBase64);

                var infoManifestacion = new InformacionManifestacion
                {
                    firmaElectronica = new Vucem.Microservice.Api.VucemIngresoAPI.FirmaElectronica
                    {
                        cadenaOriginal = peticion.CadenaOriginal,
                        firma = firmaBytes,
                        certificado = certBytes
                    },
                    importadorexportador = new ImportadorExportador { rfc = peticion.RfcImportador }
                };

                var textEncoding = new System.ServiceModel.Channels.TextMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
                var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
                var binding = new System.ServiceModel.Channels.CustomBinding(textEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

                var endpoint = new System.ServiceModel.EndpointAddress("https://privados.ventanillaunica.gob.mx/IngresoManifestacionImpl/IngresoManifestacionService");
                var client = new IngresoManifestacionRemoteClient(binding, endpoint);

                var mustUnderstand = client.Endpoint.Behaviors.Find<System.ServiceModel.Description.MustUnderstandBehavior>();
                if (mustUnderstand != null) mustUnderstand.ValidateMustUnderstand = false;

                client.Endpoint.Behaviors.Add(new VucemEndpointBehavior(peticion.UsuarioWcf, peticion.PasswordWcf));

                var respuesta = await client.registroManifestacionAsync(infoManifestacion);
                return Ok(respuesta);
            }
            catch (FaultException ex) { return BadRequest($"Error de Negocio VUCEM: {ex.Message}"); }
            catch (Exception ex) { return InternalServerError(ex); }
        }

       
        [HttpPost]
        [Route("digitalizar")]
        public async Task<IHttpActionResult> DigitalizarDocumento([FromBody] PeticionDigitalizacionDto peticion)
        {
            try
            {
                if (peticion == null) return BadRequest("El JSON enviado es inválido.");

                System.Net.ServicePointManager.Expect100Continue = false;
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                byte[] firmaBytes = ConvertirBase64Seguro(peticion.FirmaBase64);
                byte[] certBytes = ConvertirBase64Seguro(peticion.CertificadoBase64);
                byte[] archivoPdfBytes = ConvertirBase64Seguro(peticion.ArchivoBase64);

                var peticionVucem = new Vucem.Microservice.Api.VucemDigitalizacionAPI.RegistroDigitalizarDocumentoRequest
                {
                    correoElectronico = peticion.CorreoEmail,
                    documento = new Vucem.Microservice.Api.VucemDigitalizacionAPI.Documento
                    {
                        idTipoDocumento = peticion.IdTipoDocumento,
                        nombreDocumento = peticion.NombreDocumento,
                        rfcConsulta = peticion.RfcConsulta,
                        archivo = archivoPdfBytes
                    },
                    peticionBase = new Vucem.Microservice.Api.VucemDigitalizacionAPI.PeticionBase
                    {
                        firmaElectronica = new Vucem.Microservice.Api.VucemDigitalizacionAPI.FirmaElectronica
                        {
                            cadenaOriginal = peticion.CadenaOriginal,
                            firma = firmaBytes,
                            certificado = certBytes
                        }
                    }
                };

                var mtomEncoding = new System.ServiceModel.Channels.MtomMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
                var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
                var binding = new System.ServiceModel.Channels.CustomBinding(mtomEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

                var endpoint = new System.ServiceModel.EndpointAddress("https://www.ventanillaunica.gob.mx/ventanilla/DigitalizarDocumentoService");
                var client = new ReceptorClient(binding, endpoint);

                var mustUnderstand = client.Endpoint.Behaviors.Find<System.ServiceModel.Description.MustUnderstandBehavior>();
                if (mustUnderstand != null) mustUnderstand.ValidateMustUnderstand = false;

                client.Endpoint.Behaviors.Add(new VucemEndpointBehavior(peticion.UsuarioWcf, peticion.PasswordWcf));

                var respuesta = await client.RegistroDigitalizarDocumentoAsync(peticionVucem);

                if (respuesta.registroDigitalizarDocumentoServiceResponse != null)
                {
                    return Ok(respuesta.registroDigitalizarDocumentoServiceResponse);
                }

                return Ok(respuesta);
            }
            catch (FaultException ex) { return BadRequest($"Error de Negocio VUCEM: {ex.Message}"); }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        
        private byte[] ConvertirBase64Seguro(string base64String)
        {
            if (string.IsNullOrWhiteSpace(base64String))
                throw new Exception("Una de las cadenas Base64 viene vacía o nula.");

            // Destruimos cualquier basura invisible (espacios, saltos de linea, tabs)
            string limpio = base64String.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "");

            if (limpio.Contains(","))
            {
                limpio = limpio.Substring(limpio.IndexOf(",") + 1);
            }

            int mod4 = limpio.Length % 4;
            if (mod4 > 0)
            {
                limpio += new string('=', 4 - mod4);
            }

            return Convert.FromBase64String(limpio);
        }
    }


   
}