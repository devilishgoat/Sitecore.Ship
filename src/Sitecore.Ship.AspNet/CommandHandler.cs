using System.Net;
using System.Web;

namespace Sitecore.Ship.AspNet
{
    public abstract class CommandHandler
    {
        protected CommandHandler Successor;

        public void SetSuccessor(CommandHandler successor)
        {
            Successor = successor;
        }

        public abstract void HandleRequest(HttpContextBase context);

        protected void JsonResponse(string json, bool errorOccured, bool warningOccured, HttpContextBase context)
        {
            JsonResponse(json, GetHttpStatusCode(errorOccured, warningOccured), context);
        }

        protected void JsonResponse(string json, HttpStatusCode statusCode , HttpContextBase context)
        {
            JsonResponse(json, (int) statusCode, context);
        }

        protected void JsonResponse(string json, int statusCode, HttpContextBase context)
        {
            context.Response.StatusCode = statusCode;
            context.Response.Clear();
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Write(json);
        }

        protected void TextResponse(string text, HttpStatusCode statusCode, HttpContextBase context)
        {
            TextResponse(text, (int)statusCode, context);
        }

        protected void TextResponse(string text, int statusCode, HttpContextBase context)
        {
            context.Response.StatusCode = statusCode;
            context.Response.Clear();
            context.Response.ContentType = "text/plain; charset=utf-8";
            context.Response.Write(text);
        }

        protected int GetHttpStatusCode(bool errorOccured, bool warningOccured)
        {
            if (errorOccured) return (int) 299; // Custom code to indicate error and still return json
            else if (warningOccured) return (int) HttpStatusCode.Accepted; // Warning
            else return (int) HttpStatusCode.Created;
        }
    }
}