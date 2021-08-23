namespace GlassTL.Telegram.MTProto
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    public class TLSchema : DynamicObject // ToDo: Also add support for IEnumerable
    {
        /// <summary>
        /// The schema as read from the layer schema resource file
        /// </summary>
        private static JObject _schema;
        /// <summary>
        /// The reserved methods as read from the layer schema resource file
        /// </summary>
        private static string[] _reservedMethods;
        /// <summary>
        /// Reads and parses the schema data, if not done already, and returns the parsed data
        /// 
        /// You likely don't want to use this...
        /// </summary>
        public static JObject Schema {
            get {
                if (_schema == null)
                {
                    foreach (var name in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                    {
                        if (name.EndsWith("schema.json"))
                        {
                            _schema = (JObject)JToken.ReadFrom(new JsonTextReader(new StreamReader(
                                Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                            )));
                        }
                    }
                }
                
                return _schema ?? throw new Exception("Cannot load the schema json file.");
            }
        }

        private static int Layer() => (int)Schema["schema_info"]?["layer"];

        /// <summary>
        /// Contains all previous methods.
        /// 
        /// NOTE: This intentionally does not contain the last method so that users can
        /// do something like this:
        /// 
        /// dynamic schema = new <see cref="TLSchema"/>();
        /// using (var messages = schema.messages)
        /// {
        ///     messages.getMessages(...)
        ///     messages.deleteMessages(...)
        /// }
        /// </summary>
        private List<string> _methodStack = new();
        /// <summary>
        /// Contains a static list of methods that notate groups of members.
        /// 
        /// NOTE: This is static and does not update with the schema.
        /// </summary>
        private static IEnumerable<string> ReservedMethods
        {
            get
            {
                return _reservedMethods ??= Schema["schema_info"]?["reserved"]?.ToObject<string[]>();
            }
        }

        /// <summary>
        /// Handles members that are not being invoked.
        /// 
        /// TLObjects.foo       -- Not Invoked
        /// TLObjects.foo.bar   -- Not Invoked
        /// TLObjects.foo()     -- Invoked
        /// TLObjects.foo.bar() -- Invoked
        /// 
        /// This function assigns a new instance of itself to <paramref name="result"/> while saving all previous members
        /// </summary>
        /// <param name="binder">Information about the member</param>
        /// <param name="result">The member itself</param>
        /// <returns>True if the member could be found.  Otherwise, false.</returns>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (binder.Name.ToLower() == "layer")
            {
                result = Layer();
                return true;
            }

            // If there's more to come, return a NEW instance so we can continue parsing while
            // retaining all previous information. and to keep the methods separate which allows
            // this instance to remain free of junk from previous calls.
            if (ReservedMethods.Any(reservedMethod => reservedMethod == binder.Name.ToLower()))
            {
                result = new TLSchema
                {
                    _methodStack = new List<string>(_methodStack) { binder.Name }
                };

                // Since the method is reserved, we need to continue before creating the object
                return true;
            }

            // Attempt to parse the item for return.
            // NOTE: We are returning an item here in case it's a constructor with no methods.
            result = TLObject.BuildTLObject($"{string.Join(".", _methodStack)}.{binder.Name}".Trim('.'));

            // Return whether or not the TLObject was found
            return result != null;
        }
        /// <summary>
        /// Handles members that are being invoked.
        /// 
        /// TLObjects.foo       -- Not Invoked
        /// TLObjects.foo.bar   -- Not Invoked
        /// TLObjects.foo()     -- Invoked
        /// TLObjects.foo.bar() -- Invoked
        /// 
        /// This function assigns the resulting TLObject to <paramref name="result"/> with all args saved.
        /// </summary>
        /// <param name="binder">Information about the member</param>
        /// <param name="args">Arguments to be passed to the member</param>
        /// <param name="result">The member itself</param>
        /// <returns>True if the member could be found.  Otherwise, false.</returns>
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            // Ensure that there will something so that we can pass args[0]
            args ??= new object[1];
            args[0] ??= new object();

            // Attempt to parse the item for return.
            // NOTE: All args besides the first are ignored
            result = TLObject.BuildTLObject($"{string.Join(".", _methodStack)}.{binder.Name}".Trim('.'), args[0]);

            // Return whether or not the TLObject was found
            return result != null;
        }
    }
}
