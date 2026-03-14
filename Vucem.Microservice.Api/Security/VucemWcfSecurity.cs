using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Xml;

namespace Vucem.Microservice.Api.Security
{
    public class VucemSecurityHeader : MessageHeader
    {
        private readonly string _username;
        private readonly string _password;

        public VucemSecurityHeader(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public override string Name => "Security";
        public override string Namespace => "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
        public override bool MustUnderstand => true;

        protected override void OnWriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            writer.WriteStartElement("wsse", Name, Namespace);
            writer.WriteAttributeString("s", "mustUnderstand", "http://schemas.xmlsoap.org/soap/envelope/", "1");
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            string wsuNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

            // INYECTAR EL RELOJ (TIMESTAMP) DINÁMICO
            writer.WriteStartElement("wsu", "Timestamp", wsuNamespace);
            writer.WriteAttributeString("wsu", "Id", wsuNamespace, "TS-" + Guid.NewGuid().ToString("N"));

            DateTime horaActual = DateTime.UtcNow;
            writer.WriteStartElement("wsu", "Created", wsuNamespace);
            writer.WriteString(horaActual.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            writer.WriteEndElement();

            writer.WriteStartElement("wsu", "Expires", wsuNamespace);
            writer.WriteString(horaActual.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            writer.WriteEndElement();
            writer.WriteEndElement(); // Fin Timestamp

            // INYECTAR USUARIO Y CONTRASEÑA
            writer.WriteStartElement("wsse", "UsernameToken", Namespace);
            writer.WriteAttributeString("xmlns", "wsu", null, wsuNamespace);
            writer.WriteAttributeString("wsu", "Id", wsuNamespace, "UsernameToken-1");

            writer.WriteStartElement("wsse", "Username", Namespace);
            writer.WriteString(_username);
            writer.WriteEndElement();

            writer.WriteStartElement("wsse", "Password", Namespace);
            writer.WriteAttributeString("Type", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText");
            writer.WriteString(_password);
            writer.WriteEndElement();

            writer.WriteEndElement(); // Fin UsernameToken
        }
    }

    public class VucemInspector : IClientMessageInspector
    {
        private readonly string _usuario;
        private readonly string _contrasena;

        public VucemInspector(string usuario, string contrasena)
        {
            _usuario = usuario;
            _contrasena = contrasena;
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            // Limpiamos la acción basura que pueda meter Microsoft
            request.Headers.RemoveAll("Action", "http://schemas.microsoft.com/ws/2005/05/addressing/none");

            // Inyectamos nuestro encabezado perfecto
            request.Headers.Add(new VucemSecurityHeader(_usuario, _contrasena));

            return null;
        }

        public void AfterReceiveReply(ref Message reply, object correlationState) { }
    }

    public class VucemEndpointBehavior : IEndpointBehavior
    {
        private readonly string _usuario;
        private readonly string _contrasena;

        public VucemEndpointBehavior(string usuario, string contrasena)
        {
            _usuario = usuario;
            _contrasena = contrasena;
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters) { }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.ClientMessageInspectors.Add(new VucemInspector(_usuario, _contrasena));
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher) { }
        public void Validate(ServiceEndpoint endpoint) { }
    }
}