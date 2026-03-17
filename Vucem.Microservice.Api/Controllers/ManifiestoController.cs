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
                    importadorexportador = new ImportadorExportador { rfc = peticion.RfcImportador },
                    datosManifestacionValor = new Vucem.Microservice.Api.VucemIngresoAPI.DatosManifestacionValor
                    {
                        // ... Mapear aquí todas las propiedades requeridas por VUCEM (conceptos, valores, tipo de operación, etc.)
                        // Estas propiedades deberían venir de tu objeto 'peticion' (PeticionVucemDto)
                    }
                };

                var textEncoding = new System.ServiceModel.Channels.TextMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
                var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
                var binding = new System.ServiceModel.Channels.CustomBinding(textEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

                var endpoint = new System.ServiceModel.EndpointAddress("https://privados.ventanillaunica.gob.mx/IngresoManifestacionImpl/IngresoManifestacionService?wsdl");
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



        // =========================================================================
        // 3. CONSULTAR E-DOCUMENT (Si el SAT se tardó en digitalizar el PDF)
        // =========================================================================
        [HttpPost]
        [Route("digitalizar/consultar")]
        public async Task<IHttpActionResult> ConsultarDigitalizacion([FromBody] PeticionConsultaDigitalizacionDto peticion)
        {
            try
            {
                if (peticion == null) return BadRequest("JSON inválido.");

                System.Net.ServicePointManager.Expect100Continue = false;
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                byte[] firmaBytes = ConvertirBase64Seguro(peticion.FirmaBase64);
                byte[] certBytes = ConvertirBase64Seguro(peticion.CertificadoBase64);

                // Armamos el objeto nativo
                var peticionVucem = new Vucem.Microservice.Api.VucemDigitalizacionAPI.ConsultaDigitalizarDocumentoRequest
                {
                    numeroOperacion = peticion.NumeroOperacion,
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

                // Usamos codificación MTOM al igual que en la subida del archivo
                var mtomEncoding = new System.ServiceModel.Channels.MtomMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
                var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
                var binding = new System.ServiceModel.Channels.CustomBinding(mtomEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

                var endpoint = new System.ServiceModel.EndpointAddress("https://www.ventanillaunica.gob.mx/ventanilla/DigitalizarDocumentoService");
                var client = new ReceptorClient(binding, endpoint);

                var mustUnderstand = client.Endpoint.Behaviors.Find<System.ServiceModel.Description.MustUnderstandBehavior>();
                if (mustUnderstand != null) mustUnderstand.ValidateMustUnderstand = false;

                client.Endpoint.Behaviors.Add(new VucemEndpointBehavior(peticion.UsuarioWcf, peticion.PasswordWcf));

                var respuesta = await client.ConsultaEDocumentDigitalizarDocumentoAsync(peticionVucem);
                return Ok(respuesta);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // =========================================================================
        // 4. CONSULTAR COVE (Ajustado al Reference.cs real)
        // =========================================================================
        [HttpPost]
        [Route("cove/consultar")]
        public async Task<IHttpActionResult> ConsultarCove([FromBody] PeticionConsultaCoveDto peticion)
        {
            try
            {
                if (peticion == null) return BadRequest("JSON inválido.");

                System.Net.ServicePointManager.Expect100Continue = false;
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                // 1. Armamos el objeto nativo exactamente como lo pide el Reference.cs
                var peticionVucem = new VucemConsultaAPI.datosManifestacionGeneral
                {
                    eDocument = peticion.EDocumentCove
                };

                // Si el usuario decide mandar el Número de Operación en lugar del COVE
                if (peticion.NumeroOperacion.HasValue)
                {
                    peticionVucem.numeroOperacion = peticion.NumeroOperacion.Value;
                    peticionVucem.numeroOperacionSpecified = true; // REQUISITO DE WCF PARA ENVIAR NÚMEROS
                }

                // 2. Armamos la Red
                var textEncoding = new System.ServiceModel.Channels.TextMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
                var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
                var binding = new System.ServiceModel.Channels.CustomBinding(textEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

                // URL directa del WSDL que me compartiste
                var endpoint = new System.ServiceModel.EndpointAddress("https://privados.ventanillaunica.gob.mx/ConsultaManifestacionImpl/ConsultaManifestacionService?wsdl");
                // 3. El Cliente generado por Visual Studio
                var client = new VucemConsultaAPI.ConsultaManifestacionRemoteClient(binding, endpoint);

                // 4. Candado MustUnderstand (Ignorar el encabezado de seguridad del Acuse)
                var mustUnderstand = client.Endpoint.Behaviors.Find<System.ServiceModel.Description.MustUnderstandBehavior>();
                if (mustUnderstand != null) mustUnderstand.ValidateMustUnderstand = false;

                // 5. Inyectamos las credenciales de seguridad (Usuario y Password WCF)
                client.Endpoint.Behaviors.Add(new VucemEndpointBehavior(peticion.UsuarioWcf, peticion.PasswordWcf));

                // 6. Ejecutamos la consulta asíncrona
                var respuesta = await client.consultaManifestacionAsync(peticionVucem);

                // Desempaquetamos la propiedad @return para mandar un JSON limpio a .NET 8
                if (respuesta != null && respuesta.@return != null)
                {
                    return Ok(respuesta.@return);
                }

                return Ok(respuesta);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =========================================================================
        // 5. ACTUALIZAR COVE (Para corregir errores materiales en el Expediente)
        // =========================================================================
        //[HttpPost]
        //[Route("cove/actualizar")]
        //public async Task<IHttpActionResult> ActualizarCove([FromBody] PeticionVucemDto peticion)
        //{
        //    try
        //    {
        //        if (peticion == null) return BadRequest("JSON inválido.");

        //        System.Net.ServicePointManager.Expect100Continue = false;
        //        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

        //        byte[] firmaBytes = ConvertirBase64Seguro(peticion.FirmaBase64);
        //        byte[] certBytes = ConvertirBase64Seguro(peticion.CertificadoBase64);

        //        // Reutilizamos el mismo objeto que el de Ingreso (Suelen ser idénticos)
        //        var infoManifestacion = new VucemActualizarAPI.InformacionManifestacion
        //        {
        //            firmaElectronica = new VucemActualizarAPI.FirmaElectronica
        //            {
        //                cadenaOriginal = peticion.CadenaOriginal,
        //                firma = firmaBytes,
        //                certificado = certBytes
        //            },
        //            importadorexportador = new VucemActualizarAPI.ImportadorExportador { rfc = peticion.RfcImportador }
        //        };

        //        var textEncoding = new System.ServiceModel.Channels.TextMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
        //        var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
        //        var binding = new System.ServiceModel.Channels.CustomBinding(textEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

        //        var endpoint = new System.ServiceModel.EndpointAddress("https://privados.ventanillaunica.gob.mx/ActualizarManifestacionImpl/ActualizarManifestacionService");
        //        var client = new VucemActualizarAPI.ActualizarManifestacionRemoteClient(binding, endpoint);

        //        var mustUnderstand = client.Endpoint.Behaviors.Find<System.ServiceModel.Description.MustUnderstandBehavior>();
        //        if (mustUnderstand != null) mustUnderstand.ValidateMustUnderstand = false;

        //        client.Endpoint.Behaviors.Add(new VucemEndpointBehavior(peticion.UsuarioWcf, peticion.PasswordWcf));

        //        var respuesta = await client.actualizarManifestacionAsync(infoManifestacion);
        //        return Ok(respuesta);
        //    }
        //    catch (Exception ex) { return InternalServerError(ex); }
        //}


        // =========================================================================
        // 6. CONSULTAR CATÁLOGO DE TIPOS DE DOCUMENTO (Para llenar los Dropdowns)
        // =========================================================================
        [HttpPost]
        [Route("digitalizar/tipos-documento")]
        public async Task<IHttpActionResult> ConsultarTiposDocumento([FromBody] PeticionConsultaTiposDocDto peticion)
        {
            try
            {
                if (peticion == null) return BadRequest("JSON inválido.");

                System.Net.ServicePointManager.Expect100Continue = false;
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                // 1. Convertimos la FIEL
                byte[] firmaBytes = ConvertirBase64Seguro(peticion.FirmaBase64);
                byte[] certBytes = ConvertirBase64Seguro(peticion.CertificadoBase64);

                // 2. Armamos la petición con la FIEL obligatoria
                var peticionVucem = new Vucem.Microservice.Api.VucemDigitalizacionAPI.ConsultaTipoDeDocumentoRequest
                {
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

                // 3. MTOM siempre para Digitalización
                var mtomEncoding = new System.ServiceModel.Channels.MtomMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
                var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
                var binding = new System.ServiceModel.Channels.CustomBinding(mtomEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

                var endpoint = new System.ServiceModel.EndpointAddress("https://www.ventanillaunica.gob.mx/ventanilla/DigitalizarDocumentoService");
                var client = new ReceptorClient(binding, endpoint);

                var mustUnderstand = client.Endpoint.Behaviors.Find<System.ServiceModel.Description.MustUnderstandBehavior>();
                if (mustUnderstand != null) mustUnderstand.ValidateMustUnderstand = false;

                // 4. Inyectamos la seguridad de Transporte (WCF)
                client.Endpoint.Behaviors.Add(new VucemEndpointBehavior(peticion.UsuarioWcf, peticion.PasswordWcf));

                var respuesta = await client.ConsultaTipoDeDocumentoAsync(peticionVucem);

                if (respuesta != null && respuesta.consultaTipoDocumentoServiceResponse != null)
                {
                    return Ok(respuesta.consultaTipoDocumentoServiceResponse);
                }

                return Ok(respuesta);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
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