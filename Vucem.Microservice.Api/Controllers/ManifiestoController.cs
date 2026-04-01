using                    Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Vucem.Microservice.Api.Dto;
using Vucem.Microservice.Api.Security;
using Vucem.Microservice.Api.VucemDigitalizacionAPI; 
using Vucem.Microservice.Api.VucemIngresoAPI;


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

                // 1. Convertir Base64 a Bytes (Usando las llaves, NO la firma vieja)
                byte[] certBytes = ConvertirBase64Seguro(peticion.CertificadoBase64);
                byte[] keyBytes = ConvertirBase64Seguro(peticion.LlavePrivadaBase64);

                // 2. Firmar la Cadena Original en este preciso momento usando BouncyCastle
                string cadenaOriginalSegura = peticion.CadenaOriginal ?? "||";
                byte[] firmaBytes = FirmarCadenaConBouncyCastle(cadenaOriginalSegura, keyBytes, peticion.PasswordLlave);

                // 3. Armar el objeto para WCF
                var infoManifestacion = new InformacionManifestacion
                {
                    firmaElectronica = new Vucem.Microservice.Api.VucemIngresoAPI.FirmaElectronica
                    {
                        cadenaOriginal = cadenaOriginalSegura,
                        firma = firmaBytes,
                        certificado = certBytes
                    },
                    importadorexportador = new ImportadorExportador { rfc = peticion.RfcImportador },
                    datosManifestacionValor = new Vucem.Microservice.Api.VucemIngresoAPI.DatosManifestacionValor
                    {
                        // 1. PERSONAS A CONSULTAR
                        personaConsulta = peticion.PersonasConsulta?.Select(rfc => new Vucem.Microservice.Api.VucemIngresoAPI.PersonaConsulta
                        {
                            rfc = rfc.RfcConsulta, // Propiedad validada en Reference.cs
                            tipoFigura = rfc.TipoFigura
                        }).ToArray(),

                        // 2. TOTALES GLOBALES (Se encapsulan en el objeto ValorEnAduana)
                        valorEnAduana = new Vucem.Microservice.Api.VucemIngresoAPI.ValorEnAduana
                        {
                            totalValorAduana = peticion.TotalValorAduana,
                            totalPrecioPagado = peticion.TotalPrecioPagado,
                            totalPrecioPorPagar = peticion.TotalPrecioPorPagar,
                            totalIncrementables = peticion.TotalIncrementables,
                            totalDecrementables = peticion.TotalDecrementables
                        },

                        // 3. COVES, FACTURAS Y CONCEPTOS
                        informacionCove = peticion.Coves?.Select(c => new Vucem.Microservice.Api.VucemIngresoAPI.InformacionCove
                        {
                            // Datos base del COVE
                            cove = c.NumeroCove,
                            incoterm = c.Incoterm,
                            existeVinculacion = c.ExisteVinculacion ? 1 : 0,
                            metodoValoracion = c.MetodoValoracion,

                            // Objeto Pedimento anidado
                            pedimento = string.IsNullOrEmpty(c.NumeroPedimento) ? null : new Vucem.Microservice.Api.VucemIngresoAPI.Pedimento[]
                            {
                new Vucem.Microservice.Api.VucemIngresoAPI.Pedimento
                {
                    pedimento = c.NumeroPedimento,
                    aduana = c.Aduana,
                    patente = c.Patente
                }
                            },

                            // Listas de Precios Pagados
                            precioPagado = c.PreciosPagados?.Select(p => new Vucem.Microservice.Api.VucemIngresoAPI.PrecioPagado
                            {
                                total = p.Total,
                                fechaPago = DateTime.SpecifyKind(p.FechaPago.Value, DateTimeKind.Unspecified),
                                tipoPago = p.TipoPago,
                                tipoMoneda = p.TipoMoneda,
                                tipoCambio = p.TipoCambio,
                                especifique = p.DescripcionOtroPago
                            }).ToArray(),

                            // Listas de Precios Por Pagar
                            precioPorPagar = c.PreciosPorPagar?.Select(p => new Vucem.Microservice.Api.VucemIngresoAPI.PrecioPorPagar
                            {
                                total = p.Total,
                                fechaPago = DateTime.SpecifyKind(p.FechaPago.Value, DateTimeKind.Unspecified),
                                situacionNofechaPago = p.SituacionNoFechaPago,
                                tipoPago = p.TipoPago,
                                tipoMoneda = p.TipoMoneda,
                                tipoCambio = p.TipoCambio,
                                especifique = p.DescripcionOtroPago
                            }).ToArray(),

                            // Compensaciones
                            compensoPago = c.Compensaciones?.Select(comp => new Vucem.Microservice.Api.VucemIngresoAPI.CompensoPago
                            {
                                fecha = DateTime.SpecifyKind(comp.Fecha.Value, DateTimeKind.Unspecified),
                                motivo = comp.Motivo,
                                prestacionMercancia = comp.PrestacionMercancia,
                                tipoPago = comp.TipoPago,
                                especifique = comp.DescripcionOtroPago
                            }).ToArray(),

                            // Incrementables
                            incrementables = c.Incrementables?.Select(inc => new Vucem.Microservice.Api.VucemIngresoAPI.Incrementables
                            {
                                tipoIncrementable = inc.ClaveConcepto,
                                importe = inc.Importe,
                                fechaErogacion = DateTime.SpecifyKind(inc.FechaErogacion.Value, DateTimeKind.Unspecified),
                                tipoMoneda = inc.TipoMoneda,
                                tipoCambio = inc.TipoCambio,
                                aCargoImportador = inc.AcargoImportador ? 1 : 0
                            }).ToArray(),

                            // Decrementables
                            decrementables = c.Decrementables?.Select(dec => new Vucem.Microservice.Api.VucemIngresoAPI.Decrementables
                            {
                                tipoDecrementable = dec.ClaveConcepto,
                                importe = dec.Importe,
                                fechaErogacion = DateTime.SpecifyKind(dec.FechaErogacion.Value, DateTimeKind.Unspecified),
                                tipoMoneda = dec.TipoMoneda,
                                tipoCambio = dec.TipoCambio
                            }).ToArray()

                        }).ToArray()
                    },
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

        // =========================================================================
        // MÉTODO AUXILIAR DE BOUNCYCASTLE PARA FIRMAR (Agrégalo en tu controlador)
        // =========================================================================
        private byte[] FirmarCadenaConBouncyCastle(string cadenaOriginal, byte[] llavePrivadaBytes, string password)
        {
            try
            {
                // 1. Desencriptar el archivo .key con la contraseña
                Org.BouncyCastle.Crypto.AsymmetricKeyParameter privateKey =
                    Org.BouncyCastle.Security.PrivateKeyFactory.DecryptKey(password.ToCharArray(), llavePrivadaBytes);

                // 2. Configurar el firmante SHA256 (Exigido por el SAT)
                var signer = Org.BouncyCastle.Security.SignerUtilities.GetSigner("SHA256withRSA");
                signer.Init(true, privateKey);

                // 3. Convertir la cadena original a bytes UTF8 y procesar
                byte[] cadenaBytes = System.Text.Encoding.UTF8.GetBytes(cadenaOriginal);
                signer.BlockUpdate(cadenaBytes, 0, cadenaBytes.Length);

                // 4. Devolver la firma resultante
                return signer.GenerateSignature();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al intentar generar la firma criptográfica con la e.firma: {ex.Message}");
            }
        }


        //[HttpPost]
        //[Route("digitalizar")]
        //public async Task<IHttpActionResult> DigitalizarDocumento([FromBody] PeticionDigitalizacionDto peticion)
        //{
        //    try
        //    {
        //        if (peticion == null) return BadRequest("El JSON enviado es inválido.");

        //        System.Net.ServicePointManager.Expect100Continue = false;
        //        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

        //        byte[] firmaBytes = ConvertirBase64Seguro(peticion.FirmaBase64);
        //        byte[] certBytes = ConvertirBase64Seguro(peticion.CertificadoBase64);
        //        byte[] archivoPdfBytes = ConvertirBase64Seguro(peticion.ArchivoBase64);

        //        var peticionVucem = new Vucem.Microservice.Api.VucemDigitalizacionAPI.RegistroDigitalizarDocumentoRequest
        //        {
        //            correoElectronico = peticion.CorreoEmail,
        //            documento = new Vucem.Microservice.Api.VucemDigitalizacionAPI.Documento
        //            {
        //                idTipoDocumento = peticion.IdTipoDocumento,
        //                nombreDocumento = peticion.NombreDocumento,
        //                rfcConsulta = peticion.RfcConsulta,
        //                archivo = archivoPdfBytes
        //            },
        //            peticionBase = new Vucem.Microservice.Api.VucemDigitalizacionAPI.PeticionBase
        //            {
        //                firmaElectronica = new Vucem.Microservice.Api.VucemDigitalizacionAPI.FirmaElectronica
        //                {
        //                    cadenaOriginal = peticion.CadenaOriginal,
        //                    firma = firmaBytes,
        //                    certificado = certBytes
        //                }
        //            }
        //        };

        //        var mtomEncoding = new System.ServiceModel.Channels.MtomMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
        //        var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
        //        var binding = new System.ServiceModel.Channels.CustomBinding(mtomEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

        //        var endpoint = new System.ServiceModel.EndpointAddress("https://www.ventanillaunica.gob.mx/ventanilla/DigitalizarDocumentoService");
        //        var client = new ReceptorClient(binding, endpoint);

        //        var mustUnderstand = client.Endpoint.Behaviors.Find<System.ServiceModel.Description.MustUnderstandBehavior>();
        //        if (mustUnderstand != null) mustUnderstand.ValidateMustUnderstand = false;

        //        client.Endpoint.Behaviors.Add(new VucemEndpointBehavior(peticion.UsuarioWcf, peticion.PasswordWcf));

        //        var respuesta = await client.RegistroDigitalizarDocumentoAsync(peticionVucem);

        //        if (respuesta.registroDigitalizarDocumentoServiceResponse != null)
        //        {
        //            return Ok(respuesta.registroDigitalizarDocumentoServiceResponse);
        //        }

        //        return Ok(respuesta);
        //    }
        //    catch (FaultException ex) { return BadRequest($"Error de Negocio VUCEM: {ex.Message}"); }
        //    catch (Exception ex) { return InternalServerError(ex); }
        //}

        //[HttpGet]
        //[Route("ejecutar2")]
        //public async Task<IHttpActionResult> EjecutarHardcodeado()
        //{
        //    try
        //    {
        //        // ==========================================
        //        // 1. DATOS HARDCODEADOS
        //        // ==========================================
        //        string usuarioWcf = "QAO680613E91";
        //        string passwordWcf = "k5QPKge7lTUFNsgjsM4I2oirN2wO67/09qlaNPgpmLXl1cB0Ov9e8Td2JjfBi1Nh";

        //        string rfcSolicitante = "QAO680613E91";
        //        string correo = "javiercitonandez@gmail.com";
        //        int idTipoDocumento = 170;
        //        string nombreDocumento = "Factura_Prueba_02"; // El nombre DEBE IR SIN la extensión .pdf
        //        string rfcConsulta = "RERA540709F98";
        //        string passwordLlave = "Qualtia23";

        //        System.Net.ServicePointManager.Expect100Continue = false;
        //        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

        //        // ==========================================
        //        // 2. LECTURA DE ARCHIVOS LOCALES
        //        // ==========================================
        //        string rutaBase = @"C:\key_test\";

        //        // REEMPLAZA ESTOS NOMBRES POR LOS NOMBRES REALES DE TUS ARCHIVOS
        //        string nombreCer = "00001000000517319189.cer";
        //        string nombreKey = "CSD_VUCEM_QAO680613E91_20230119_105407.key";
        //        string nombrePdf = "Doc1.pdf";

        //        byte[] certBytes = System.IO.File.ReadAllBytes(rutaBase + nombreCer);
        //        byte[] llavePrivadaBytes = System.IO.File.ReadAllBytes(rutaBase + nombreKey);
        //        byte[] archivoPdfBytes = System.IO.File.ReadAllBytes(rutaBase + nombrePdf);

        //        // ==========================================
        //        // 3. CREACIÓN DEL HASH Y CADENA ORIGINAL
        //        // ==========================================
        //        string hashPdfHex;
        //        using (SHA1 sha1 = SHA1.Create())
        //        {
        //            byte[] hashBytes = sha1.ComputeHash(archivoPdfBytes);
        //            hashPdfHex = string.Concat(hashBytes.Select(b => b.ToString("x2")));
        //        }

        //        StringBuilder cadenaBuilder = new StringBuilder();
        //        cadenaBuilder.Append("|").Append(rfcSolicitante);
        //        cadenaBuilder.Append("|").Append(correo);
        //        cadenaBuilder.Append("|").Append(idTipoDocumento);
        //        cadenaBuilder.Append("|").Append(nombreDocumento);
        //        // Siempre debe llevar el pipe, incluso si `rfcConsulta` está vacío
        //        cadenaBuilder.Append("|").Append(rfcConsulta ?? "");
        //        cadenaBuilder.Append("|").Append(hashPdfHex).Append("|");

        //        string cadenaOriginal = cadenaBuilder.ToString();

        //        System.Diagnostics.Debug.WriteLine(cadenaOriginal);

        //        // ==========================================
        //        // 4. FIRMADO ELECTRÓNICO CON BOUNCYCASTLE
        //        // ==========================================
        //        byte[] firmaBytes;
        //        AsymmetricKeyParameter llavePrivadaBouncy;

        //        try
        //        {
        //            llavePrivadaBouncy = PrivateKeyFactory.DecryptKey(passwordLlave.ToCharArray(), llavePrivadaBytes);
        //        }
        //        catch
        //        {
        //            return BadRequest("Error al desencriptar el archivo .key. Verifica que la contraseña sea la correcta.");
        //        }

        //        // VUCEM exige SHA256 para certificados nuevos de e.firma
        //        ISigner signer = SignerUtilities.GetSigner("SHA256WithRSA");
        //        signer.Init(true, llavePrivadaBouncy);

        //        byte[] datosAFirmar = Encoding.UTF8.GetBytes(cadenaOriginal);
        //        signer.BlockUpdate(datosAFirmar, 0, datosAFirmar.Length);

        //        firmaBytes = signer.GenerateSignature();

        //        // ==========================================
        //        // 5. LLENADO DE OBJETOS WCF 
        //        // ==========================================
        //        var peticionVucem = new Vucem.Microservice.Api.VucemDigitalizacionAPI.RegistroDigitalizarDocumentoRequest
        //        {
        //            correoElectronico = correo,
        //            documento = new Vucem.Microservice.Api.VucemDigitalizacionAPI.Documento
        //            {
        //                idTipoDocumento = idTipoDocumento,
        //                nombreDocumento = nombreDocumento,
        //                rfcConsulta = rfcConsulta,
        //                archivo = archivoPdfBytes
        //            },
        //            peticionBase = new Vucem.Microservice.Api.VucemDigitalizacionAPI.PeticionBase
        //            {
        //                firmaElectronica = new Vucem.Microservice.Api.VucemDigitalizacionAPI.FirmaElectronica
        //                {
        //                    cadenaOriginal = cadenaOriginal,
        //                    firma = firmaBytes,
        //                    certificado = certBytes
        //                }
        //            }
        //        };

        //        // ==========================================
        //        // 6. CONFIGURACIÓN DEL CANAL MTOM Y SEGURIDAD
        //        // ==========================================
        //        var mtomEncoding = new System.ServiceModel.Channels.MtomMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
        //        var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
        //        var binding = new System.ServiceModel.Channels.CustomBinding(mtomEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

        //        var endpoint = new System.ServiceModel.EndpointAddress("https://www.ventanillaunica.gob.mx/ventanilla/DigitalizarDocumentoService");

        //        var client = new ReceptorClient(binding, endpoint);

        //        var mustUnderstand = client.Endpoint.Behaviors.Find<System.ServiceModel.Description.MustUnderstandBehavior>();
        //        if (mustUnderstand != null) mustUnderstand.ValidateMustUnderstand = false;

        //        client.Endpoint.Behaviors.Add(new VucemEndpointBehavior(usuarioWcf, passwordWcf));

        //        // ==========================================
        //        // 7. DISPARO DE LA PETICIÓN
        //        // ==========================================
        //        var respuesta = await client.RegistroDigitalizarDocumentoAsync(peticionVucem);

        //        if (respuesta.registroDigitalizarDocumentoServiceResponse != null)
        //        {
        //            return Ok(respuesta.registroDigitalizarDocumentoServiceResponse);
        //        }

        //        return Ok(respuesta);
        //    }
        //    catch (FaultException ex)
        //    {
        //        return BadRequest($"Error de Negocio VUCEM: {ex.Message}");
        //    }
        //    catch (Exception ex)
        //    {
        //        return InternalServerError(ex);
        //    }
        //}


        // =========================================================================
        // 3. CONSULTAR E-DOCUMENT (Si el SAT se tardó en digitalizar el PDF)
        // =========================================================================
        //[HttpPost]
        //[Route("digitalizar/consultar")]
        //public async Task<IHttpActionResult> ConsultarDigitalizacion([FromBody] PeticionConsultaDigitalizacionDto peticion)
        //{
        //    try
        //    {
        //        if (peticion == null) return BadRequest("JSON inválido.");

        //        System.Net.ServicePointManager.Expect100Continue = false;
        //        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

        //        byte[] firmaBytes = ConvertirBase64Seguro(peticion.FirmaBase64);
        //        byte[] certBytes = ConvertirBase64Seguro(peticion.CertificadoBase64);

        //        // Armamos el objeto nativo
        //        var peticionVucem = new Vucem.Microservice.Api.VucemDigitalizacionAPI.ConsultaDigitalizarDocumentoRequest
        //        {
        //            numeroOperacion = peticion.NumeroOperacion,
        //            peticionBase = new Vucem.Microservice.Api.VucemDigitalizacionAPI.PeticionBase
        //            {
        //                firmaElectronica = new Vucem.Microservice.Api.VucemDigitalizacionAPI.FirmaElectronica
        //                {
        //                    cadenaOriginal = peticion.CadenaOriginal,
        //                    firma = firmaBytes,
        //                    certificado = certBytes
        //                }
        //            }
        //        };

        //        // Usamos codificación MTOM al igual que en la subida del archivo
        //        var mtomEncoding = new System.ServiceModel.Channels.MtomMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
        //        var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
        //        var binding = new System.ServiceModel.Channels.CustomBinding(mtomEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

        //        var endpoint = new System.ServiceModel.EndpointAddress("https://www.ventanillaunica.gob.mx/ventanilla/DigitalizarDocumentoService");
        //        var client = new ReceptorClient(binding, endpoint);

        //        var mustUnderstand = client.Endpoint.Behaviors.Find<System.ServiceModel.Description.MustUnderstandBehavior>();
        //        if (mustUnderstand != null) mustUnderstand.ValidateMustUnderstand = false;

        //        client.Endpoint.Behaviors.Add(new VucemEndpointBehavior(peticion.UsuarioWcf, peticion.PasswordWcf));

        //        var respuesta = await client.ConsultaEDocumentDigitalizarDocumentoAsync(peticionVucem);
        //        return Ok(respuesta);
        //    }
        //    catch (Exception ex) { return InternalServerError(ex); }
        //}

        //// =========================================================================
        //// 3.5. EJECUTAR CONSULTAR E-DOCUMENT HARDCODEADO (Por Número de Operación)
        //// =========================================================================
        //[HttpGet]
        //[Route("digitalizar/consultar-ejecutar2")]
        //public async Task<IHttpActionResult> EjecutarNoOperacion()
        //{
        //    try
        //    {
        //        // ==========================================
        //        // 1. DATOS DE NEGOCIO Y CREDENCIALES WCF
        //        // ==========================================
        //        string usuarioWcf = "QAO680613E91";
        //        string passwordWcf = "k5QPKge7lTUFNsgjsM4I2oirN2wO67/09qlaNPgpmLXl1cB0Ov9e8Td2JjfBi1Nh";

        //        // <-- REEMPLAZA POR EL NÚMERO DE OPERACIÓN QUE TE DIO EL ENDPOINT EJECUTAR -->
        //        long numeroOperacion = 317180576; 
        //        string passwordLlave = "Qualtia23";

        //        System.Net.ServicePointManager.Expect100Continue = false;
        //        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

        //        // ==========================================
        //        // 2. LECTURA DE ARCHIVOS DESDE LA PC
        //        // ==========================================
        //        string rutaBase = @"C:\key_test\";
        //        string nombreCer = "00001000000517319189.cer";
        //        string nombreKey = "CSD_VUCEM_QAO680613E91_20230119_105407.key";

        //        byte[] certBytes = System.IO.File.ReadAllBytes(rutaBase + nombreCer);
        //        byte[] llavePrivadaBytes = System.IO.File.ReadAllBytes(rutaBase + nombreKey);

        //        // ==========================================
        //        // 3. CONSTRUCCIÓN DE LA CADENA ORIGINAL
        //        // ==========================================
        //        // VUCEM para ConsultaEDocument exige el formato |{rfc}|{numeroOperacion}|
        //        string rfcSolicitante = "QAO680613E91";
        //        string cadenaOriginal = $"|{rfcSolicitante}|{numeroOperacion}|";

        //        // ==========================================
        //        // 4. FIRMADO CON BOUNCYCASTLE
        //        // ==========================================
        //        byte[] firmaBytes;
        //        AsymmetricKeyParameter llavePrivadaBouncy;

        //        try
        //        {
        //            llavePrivadaBouncy = PrivateKeyFactory.DecryptKey(passwordLlave.ToCharArray(), llavePrivadaBytes);
        //        }
        //        catch
        //        {
        //            return BadRequest("Error al abrir el archivo .key. Verifica la contraseña.");
        //        }

        //        // Usamos SHA256WithRSA que es el estándar actual para VUCEM e.firma
        //        ISigner signer = SignerUtilities.GetSigner("SHA256WithRSA");
        //        signer.Init(true, llavePrivadaBouncy);

        //        byte[] datosAFirmar = Encoding.UTF8.GetBytes(cadenaOriginal);
        //        signer.BlockUpdate(datosAFirmar, 0, datosAFirmar.Length);
        //        firmaBytes = signer.GenerateSignature();

        //        // ==========================================
        //        // 5. ARMADO DEL OBJETO
        //        // ==========================================
        //        var peticionVucem = new Vucem.Microservice.Api.VucemDigitalizacionAPI.ConsultaDigitalizarDocumentoRequest
        //        {
        //            numeroOperacion = numeroOperacion,
        //            peticionBase = new Vucem.Microservice.Api.VucemDigitalizacionAPI.PeticionBase
        //            {
        //                firmaElectronica = new Vucem.Microservice.Api.VucemDigitalizacionAPI.FirmaElectronica
        //                {
        //                    cadenaOriginal = cadenaOriginal,
        //                    firma = firmaBytes,
        //                    certificado = certBytes
        //                }
        //            }
        //        };

        //        // ==========================================
        //        // 6. ENVÍO AL WEB SERVICE
        //        // ==========================================
        //        var mtomEncoding = new System.ServiceModel.Channels.MtomMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
        //        var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
        //        var binding = new System.ServiceModel.Channels.CustomBinding(mtomEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

        //        var endpoint = new System.ServiceModel.EndpointAddress("https://www.ventanillaunica.gob.mx/ventanilla/DigitalizarDocumentoService");
        //        var client = new ReceptorClient(binding, endpoint);

        //        var mustUnderstand = client.Endpoint.Behaviors.Find<System.ServiceModel.Description.MustUnderstandBehavior>();
        //        if (mustUnderstand != null) mustUnderstand.ValidateMustUnderstand = false;

        //        client.Endpoint.Behaviors.Add(new VucemEndpointBehavior(usuarioWcf, passwordWcf));

        //        var respuesta = await client.ConsultaEDocumentDigitalizarDocumentoAsync(peticionVucem);
        //        return Ok(respuesta);
        //    }
        //    catch (FaultException ex)
        //    {
        //        return BadRequest($"Error de Negocio VUCEM: {ex.Message}");
        //    }
        //    catch (Exception ex)
        //    {
        //        return InternalServerError(ex);
        //    }
        //}

        // =========================================================================
        // 4. CONSULTAR COVE (Ajustado al Reference.cs real)
        // =========================================================================
        //[HttpPost]
        //[Route("cove/consultar")]
        //public async Task<IHttpActionResult> ConsultarCove([FromBody] PeticionConsultaCoveDto peticion)
        //{
        //    try
        //    {
        //        if (peticion == null) return BadRequest("JSON inválido.");

        //        System.Net.ServicePointManager.Expect100Continue = false;
        //        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

        //        // 1. Armamos el objeto nativo exactamente como lo pide el Reference.cs
        //        var peticionVucem = new VucemConsultaAPI.datosManifestacionGeneral
        //        {
        //            eDocument = peticion.EDocumentCove
        //        };

        //        // Si el usuario decide mandar el Número de Operación en lugar del COVE
        //        if (peticion.NumeroOperacion.HasValue)
        //        {
        //            peticionVucem.numeroOperacion = peticion.NumeroOperacion.Value;
        //            peticionVucem.numeroOperacionSpecified = true; // REQUISITO DE WCF PARA ENVIAR NÚMEROS
        //        }

        //        // 2. Armamos la Red
        //        var textEncoding = new System.ServiceModel.Channels.TextMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
        //        var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
        //        var binding = new System.ServiceModel.Channels.CustomBinding(textEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

        //        // URL directa del WSDL que me compartiste
        //        var endpoint = new System.ServiceModel.EndpointAddress("https://privados.ventanillaunica.gob.mx/ConsultaManifestacionImpl/ConsultaManifestacionService?wsdl");
        //        // 3. El Cliente generado por Visual Studio
        //        var client = new VucemConsultaAPI.ConsultaManifestacionRemoteClient(binding, endpoint);

        //        // 4. Candado MustUnderstand (Ignorar el encabezado de seguridad del Acuse)
        //        var mustUnderstand = client.Endpoint.Behaviors.Find<System.ServiceModel.Description.MustUnderstandBehavior>();
        //        if (mustUnderstand != null) mustUnderstand.ValidateMustUnderstand = false;

        //        // 5. Inyectamos las credenciales de seguridad (Usuario y Password WCF)
        //        client.Endpoint.Behaviors.Add(new VucemEndpointBehavior(peticion.UsuarioWcf, peticion.PasswordWcf));

        //        // 6. Ejecutamos la consulta asíncrona
        //        var respuesta = await client.consultaManifestacionAsync(peticionVucem);

        //        // Desempaquetamos la propiedad @return para mandar un JSON limpio a .NET 8
        //        if (respuesta != null && respuesta.@return != null)
        //        {
        //            return Ok(respuesta.@return);
        //        }

        //        return Ok(respuesta);
        //    }
        //    catch (Exception ex)
        //    {
        //        return InternalServerError(ex);
        //    }
        //}
        //--------------------------------------------------Metodos con DTOs------------------------------------------------

        [HttpPost]
        [Route("ejecutar")]
        public async Task<IHttpActionResult> EjecutarDigitalizacion([FromBody] PeticionEjecutarDigitalizacionDto peticion)
        {
            try
            {
                if (peticion == null) return BadRequest("El JSON enviado es inválido.");

                System.Net.ServicePointManager.Expect100Continue = false;
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                // ==========================================
                // 1. LECTURA DE ARCHIVOS DESDE EL DTO (Base64)
                // ==========================================
                byte[] certBytes = ConvertirBase64Seguro(peticion.CertificadoBase64);
                byte[] llavePrivadaBytes = ConvertirBase64Seguro(peticion.LlavePrivadaBase64);
                byte[] archivoPdfBytes = ConvertirBase64Seguro(peticion.ArchivoPdfBase64);

                if (archivoPdfBytes.Length > 2621440)
                {
                    return BadRequest("El archivo PDF supera el tamaño máximo permitido por VUCEM (aprox. 2.5MB). Reduzca la resolución del documento.");
                }
                string rfcSol = peticion.RfcSolicitante?.Trim().ToUpper();
                string correo = peticion.Correo?.Trim();
                string rfcCon = peticion.RfcConsulta?.Trim().ToUpper() ?? "";

                // Removemos la extensión .pdf si el usuario la mandó por error
                string nomDoc = peticion.NombreDocumento?.Replace(".pdf", "").Replace(".PDF", "");
                // Removemos acentos, espacios, la letra ñ, etc. (Dejamos solo letras, números, guiones y guiones bajos)
                nomDoc = System.Text.RegularExpressions.Regex.Replace(nomDoc ?? "Documento", @"[^a-zA-Z0-9_-]", "");
                // ==========================================
                // 2. CREACIÓN DEL HASH Y CADENA ORIGINAL
                // ==========================================
                string hashPdfHex;
                using (SHA1 sha1 = SHA1.Create())
                {
                    byte[] hashBytes = sha1.ComputeHash(archivoPdfBytes);
                    hashPdfHex = string.Concat(hashBytes.Select(b => b.ToString("x2")));
                }

                StringBuilder cadenaBuilder = new StringBuilder();
                cadenaBuilder.Append("|").Append(rfcSol);
                cadenaBuilder.Append("|").Append(correo);
                cadenaBuilder.Append("|").Append(peticion.IdTipoDocumento);
                cadenaBuilder.Append("|").Append(nomDoc);
                cadenaBuilder.Append("|").Append(rfcCon); // rfcCon ya es un string vacío ("") si venía nulo
                cadenaBuilder.Append("|").Append(hashPdfHex).Append("|");

                string cadenaOriginal = cadenaBuilder.ToString();

                // ==========================================
                // 3. FIRMADO ELECTRÓNICO CON BOUNCYCASTLE
                // ==========================================
                byte[] firmaBytes;
                AsymmetricKeyParameter llavePrivadaBouncy;

                try
                {
                    llavePrivadaBouncy = PrivateKeyFactory.DecryptKey(peticion.PasswordLlave.ToCharArray(), llavePrivadaBytes);
                }
                catch
                {
                    return BadRequest("Error al desencriptar el archivo .key. Verifica que la contraseña sea la correcta.");
                }

                // VUCEM exige SHA256 para certificados nuevos de e.firma
                ISigner signer = SignerUtilities.GetSigner("SHA256WithRSA");
                signer.Init(true, llavePrivadaBouncy);

                byte[] datosAFirmar = Encoding.UTF8.GetBytes(cadenaOriginal);
                signer.BlockUpdate(datosAFirmar, 0, datosAFirmar.Length);

                firmaBytes = signer.GenerateSignature();

                // ==========================================
                // 4. LLENADO DE OBJETOS WCF 
                // ==========================================
                var peticionVucem = new Vucem.Microservice.Api.VucemDigitalizacionAPI.RegistroDigitalizarDocumentoRequest
                {
                    correoElectronico = correo, // <-- Usamos la variable limpia
                    documento = new Vucem.Microservice.Api.VucemDigitalizacionAPI.Documento
                    {
                        idTipoDocumento = peticion.IdTipoDocumento,
                        nombreDocumento = nomDoc, // <-- Usamos el nombre sin .pdf y sin acentos
                                                  // VUCEM suele preferir que el nodo rfcConsulta no viaje si está vacío
                        rfcConsulta = string.IsNullOrEmpty(rfcCon) ? null : rfcCon,
                        archivo = archivoPdfBytes
                    },
                    peticionBase = new Vucem.Microservice.Api.VucemDigitalizacionAPI.PeticionBase
                    {
                        firmaElectronica = new Vucem.Microservice.Api.VucemDigitalizacionAPI.FirmaElectronica
                        {
                            cadenaOriginal = cadenaOriginal,
                            firma = firmaBytes,
                            certificado = certBytes
                        }
                    }
                };


                // ==========================================
                // 5. CONFIGURACIÓN DEL CANAL MTOM Y SEGURIDAD
                // ==========================================
                var mtomEncoding = new System.ServiceModel.Channels.MtomMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
                var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
                var binding = new System.ServiceModel.Channels.CustomBinding(mtomEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

                var endpoint = new System.ServiceModel.EndpointAddress("https://www.ventanillaunica.gob.mx/ventanilla/DigitalizarDocumentoService");

                var client = new ReceptorClient(binding, endpoint);

                var mustUnderstand = client.Endpoint.Behaviors.Find<System.ServiceModel.Description.MustUnderstandBehavior>();
                if (mustUnderstand != null) mustUnderstand.ValidateMustUnderstand = false;

                client.Endpoint.Behaviors.Add(new VucemEndpointBehavior(peticion.UsuarioWcf, peticion.PasswordWcf));

                // ==========================================
                // 6. DISPARO DE LA PETICIÓN
                // ==========================================
                var respuesta = await client.RegistroDigitalizarDocumentoAsync(peticionVucem);

                if (respuesta.registroDigitalizarDocumentoServiceResponse != null)
                {
                    return Ok(respuesta.registroDigitalizarDocumentoServiceResponse);
                }

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


        // =========================================================================
        // 3.5. EJECUTAR CONSULTAR E-DOCUMENT 
        // =========================================================================
        [HttpPost]
        [Route("digitalizar/consultar-ejecutar")]
        public async Task<IHttpActionResult> EjecutarNoOperacion([FromBody] PeticionEjecutarConsultaDto peticion)
        {
            try
            {
                if (peticion == null) return BadRequest("JSON inválido.");

                System.Net.ServicePointManager.Expect100Continue = false;
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                // ==========================================
                // 1. LECTURA DE ARCHIVOS DESDE EL DTO (Base64)
                // ==========================================
                byte[] certBytes = ConvertirBase64Seguro(peticion.CertificadoBase64);
                byte[] llavePrivadaBytes = ConvertirBase64Seguro(peticion.LlavePrivadaBase64);

                // ==========================================
                // 2. CONSTRUCCIÓN DE LA CADENA ORIGINAL
                // ==========================================
                // VUCEM para ConsultaEDocument exige el formato |{rfc}|{numeroOperacion}|
                string cadenaOriginal = $"|{peticion.RfcSolicitante}|{peticion.NumeroOperacion}|";

                // ==========================================
                // 3. FIRMADO CON BOUNCYCASTLE
                // ==========================================
                byte[] firmaBytes;
                AsymmetricKeyParameter llavePrivadaBouncy;

                try
                {
                    llavePrivadaBouncy = PrivateKeyFactory.DecryptKey(peticion.PasswordLlave.ToCharArray(), llavePrivadaBytes);
                }
                catch
                {
                    return BadRequest("Error al abrir el archivo .key. Verifica la contraseña.");
                }

                ISigner signer = SignerUtilities.GetSigner("SHA256WithRSA");
                signer.Init(true, llavePrivadaBouncy);

                byte[] datosAFirmar = Encoding.UTF8.GetBytes(cadenaOriginal);
                signer.BlockUpdate(datosAFirmar, 0, datosAFirmar.Length);
                firmaBytes = signer.GenerateSignature();

                // ==========================================
                // 4. ARMADO DEL OBJETO
                // ==========================================
                var peticionVucem = new Vucem.Microservice.Api.VucemDigitalizacionAPI.ConsultaDigitalizarDocumentoRequest
                {
                    numeroOperacion = peticion.NumeroOperacion,
                    peticionBase = new Vucem.Microservice.Api.VucemDigitalizacionAPI.PeticionBase
                    {
                        firmaElectronica = new Vucem.Microservice.Api.VucemDigitalizacionAPI.FirmaElectronica
                        {
                            cadenaOriginal = cadenaOriginal,
                            firma = firmaBytes,
                            certificado = certBytes
                        }
                    }
                };

                // ==========================================
                // 5. ENVÍO AL WEB SERVICE
                // ==========================================
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
            catch (FaultException ex)
            {
                return BadRequest($"Error de Negocio VUCEM: {ex.Message}");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost]
        [Route("actualizar-edocuments")]
        public async Task<IHttpActionResult> ActualizarEdocuments([FromBody] PeticionActualizarMvDto peticion)
        {
            try
            {
                if (peticion == null) return BadRequest("El JSON enviado es inválido.");

                System.Net.ServicePointManager.Expect100Continue = false;
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                // 1. Convertir Base64 a Bytes (Usando tu método seguro)
                byte[] certBytes = ConvertirBase64Seguro(peticion.CertificadoBase64);
                byte[] keyBytes = ConvertirBase64Seguro(peticion.LlavePrivadaBase64);

                // 2. Mapeo de Personas a Consultar
                var personasConsulta = peticion.PersonasConsulta?.Select(p => new Vucem.Microservice.Api.VucemIngresoAPI.PersonaConsulta
                {
                    rfc = p.RfcConsulta,
                    tipoFigura = p.TipoFigura
                }).ToArray();

                // 3. Armar los Datos de Actualización
                var datosActualizar = new Vucem.Microservice.Api.VucemIngresoAPI.DatosActualizarManifestacion
                {
                    numeroMV = peticion.NumeroMV,
                    documentos = peticion.Edocuments?.ToArray(),
                    personasConsulta = personasConsulta
                };

                // ==========================================================
                // 4. GENERAR CADENA ORIGINAL (Regla estricta VUCEM)
                // Formato: |NumeroMV|eDoc1|eDoc2|RFC1|Figura1|
                // ==========================================================
                var sb = new StringBuilder();
                sb.Append("|");
                sb.Append(datosActualizar.numeroMV).Append("|");

                if (datosActualizar.documentos != null)
                {
                    foreach (var doc in datosActualizar.documentos)
                    {
                        sb.Append(doc).Append("|");
                    }
                }

                if (datosActualizar.personasConsulta != null)
                {
                    foreach (var persona in datosActualizar.personasConsulta)
                    {
                        sb.Append(persona.rfc).Append("|");
                        sb.Append(persona.tipoFigura).Append("|");
                    }
                }
                string cadenaOriginal = sb.ToString();

                // 5. Firmar la Cadena Original (Usando BouncyCastle que ya tienes)
                byte[] firmaBytes = FirmarCadenaConBouncyCastle(cadenaOriginal, keyBytes, peticion.PasswordLlave);

                // 6. Armar la Petición Completa para WCF
                var request = new Vucem.Microservice.Api.VucemIngresoAPI.InformacionActualizarManifestacion
                {
                    datosActualizarManifestacion = datosActualizar,
                    firmaElectronica = new Vucem.Microservice.Api.VucemIngresoAPI.FirmaElectronica
                    {
                        certificado = certBytes,
                        cadenaOriginal = cadenaOriginal,
                        firma = firmaBytes
                    }
                };

                // 7. Configuración WCF (Mismo endpoint que el Ingreso)
                var textEncoding = new System.ServiceModel.Channels.TextMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
                var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
                var binding = new System.ServiceModel.Channels.CustomBinding(textEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

                var endpoint = new System.ServiceModel.EndpointAddress("https://privados.ventanillaunica.gob.mx/IngresoManifestacionImpl/IngresoManifestacionService?wsdl");
                var client = new VucemIngresoAPI.IngresoManifestacionRemoteClient(binding, endpoint);

                var mustUnderstand = client.Endpoint.Behaviors.Find<System.ServiceModel.Description.MustUnderstandBehavior>();
                if (mustUnderstand != null) mustUnderstand.ValidateMustUnderstand = false;

                client.Endpoint.Behaviors.Add(new VucemEndpointBehavior(peticion.UsuarioWcf, peticion.PasswordWcf));

                // 8. ENVIAR A VUCEM
                var respuesta = await client.actualizarManifestacionAsync(request);

                return Ok(respuesta);
            }
            catch (FaultException ex) { return BadRequest($"Error de Negocio VUCEM: {ex.Message}"); }
            catch (Exception ex) { return InternalServerError(ex); }
        }


        [HttpPost]
        [Route("consultar-mv")]
        public async Task<IHttpActionResult> ConsultarManifestacion([FromBody] PeticionConsultaMvDto peticion)
        {
            try
            {
                if (peticion == null) return BadRequest("JSON inválido.");

                System.Net.ServicePointManager.Expect100Continue = false;
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                // 1. Armamos el objeto nativo de VUCEM para buscar por Número de Operación
                var peticionVucem = new Vucem.Microservice.Api.VucemConsultaAPI.datosManifestacionGeneral
                {
                    numeroOperacion = peticion.NumeroOperacion,
                    numeroOperacionSpecified = true // OBLIGATORIO para que WCF serialice el número
                };

                // 2. Configurar la Red WCF (Consulta usa TextMessageEncoding, no MTOM)
                var textEncoding = new System.ServiceModel.Channels.TextMessageEncodingBindingElement(System.ServiceModel.Channels.MessageVersion.Soap11, System.Text.Encoding.UTF8);
                var httpsTransport = new System.ServiceModel.Channels.HttpsTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
                var binding = new System.ServiceModel.Channels.CustomBinding(textEncoding, httpsTransport) { SendTimeout = TimeSpan.FromMinutes(30) };

                // URL directa del WSDL de Consulta
                var endpoint = new System.ServiceModel.EndpointAddress("https://privados.ventanillaunica.gob.mx/ConsultaManifestacionImpl/ConsultaManifestacionService?wsdl");

                // El Cliente generado por Visual Studio
                var client = new Vucem.Microservice.Api.VucemConsultaAPI.ConsultaManifestacionRemoteClient(binding, endpoint);

                // 3. Candado MustUnderstand y Credenciales WSS
                var mustUnderstand = client.Endpoint.Behaviors.Find<System.ServiceModel.Description.MustUnderstandBehavior>();
                if (mustUnderstand != null) mustUnderstand.ValidateMustUnderstand = false;

                client.Endpoint.Behaviors.Add(new VucemEndpointBehavior(peticion.UsuarioWcf, peticion.PasswordWcf));

                // 4. Ejecutamos la consulta asíncrona
                var respuesta = await client.consultaManifestacionAsync(peticionVucem);

                // 5. Desempaquetamos la propiedad @return para mandar un JSON limpio a .NET 8
                if (respuesta != null && respuesta.@return != null)
                {
                    return Ok(respuesta.@return);
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