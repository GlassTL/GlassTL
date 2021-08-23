namespace GlassTL.Telegram.MTProto
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public partial class TLObject
    {
        /// <summary>
        /// Returns a TLObject based on the Constructor and arguments passed
        /// </summary>
        /// <param name="constructor">The constructor as a signed integer</param>
        /// <param name="args">The arguments used to create the TLObject</param>
        /// <returns>A compiled TLObject</returns>
        public static TLObject BuildTLObject(int constructor, object args = null)
        {
            var jToken = FindConstructor(constructor) ??
                throw new Exception($"Unknown constructor: \"{constructor}\"");

            return BuildTLObject(jToken, args);
        }
        /// <summary>
        /// Returns a TLObject based on the Constructor and arguments passed
        /// </summary>
        /// <param name="constructor">The constructor as an unsigned integer</param>
        /// <param name="args">The arguments used to create the TLObject</param>
        /// <returns>A compiled TLObject</returns>
        public static TLObject BuildTLObject(uint constructor, object args = null)
        {
            var jToken = FindConstructor(constructor) ??
                throw new Exception($"Unknown constructor: \"{constructor}\"");

            return BuildTLObject(jToken, args);
        }
        /// <summary>
        /// Returns a TLObject based on the Constructor and arguments passed
        /// </summary>
        /// <param name="constructor">The constructor name (case insensitive)</param>
        /// <param name="args">The arguments used to create the TLObject</param>
        /// <returns>A compiled TLObject</returns>
        public static TLObject BuildTLObject(string constructor, object args = null)
        {
            var jToken = FindConstructor(constructor) ??
                throw new Exception($"Unknown constructor: \"{constructor}\"");

            return BuildTLObject(jToken, args);
        }
        /// <summary>
        /// Called internally, we know/assume that <paramref name="tlSkeleton"/> is valid.
        /// 
        /// Here we attempt to build the TLObject from the skeleton -- adding all the
        /// args as needed.
        /// 
        /// ToDo: Add conversion between cases like pascal, camel, and snake.  TLObjects
        /// always use snake case for conformity to the schema.
        /// </summary>
        /// <param name="tlSkeleton">The skeleton TLObject parsed from the layer schema</param>
        /// <param name="args">Arguments that make up the TLObject</param>
        /// <returns>A compiled TLObject</returns>
        private static TLObject BuildTLObject(JToken tlSkeleton, object args = null)
        {
            // Assuming that the skeleton is valid
            var returns = JToken.FromObject(new
            {
                _ = tlSkeleton["name"]
            });

            /*
             * There is a point to be made for adding all params to the object in the case
             * that the param is not optional and intended to be left empty.  However, at
             * this time, only the params specified in the args variable are to be processed
             * and added.  If you would like a param to be empty, please specify that in
             * the arguments.
             */

            // Some objects don't have or need arguments
            if (args != null)
            {
                var serializer = new JsonSerializer();
                serializer.Converters.Add(new InternalConverter());

                // Compile the items for easier access
                var jArgs = JToken.FromObject(args, serializer);

                // Loop through each param in the skeleton
                foreach (var param in tlSkeleton["params"])
                {
                    var name = param.Value<string>("name");

                    // Add only if provided by the user
                    if (jArgs[name] == null) continue;

                    /*
                     * Here's a question... what if the arg provided doesn't match the type
                     * required by the skeleton's param?
                     * 
                     * ToDo: Add SIMPLE type verification.
                     * 
                     */

                    // Assume that the arg is valid and add it
                    returns[name] = jArgs[name];
                }
            }

            // Return whatever TLObject was compiled
            return new TLObject(returns);
        }

        //----------------------------------------------------------------------------------

        /// <summary>
        /// Returns a compiled skeleton of a specified TLObject parsed from the layer schema
        /// </summary>
        /// <param name="constructor"></param>
        private static JObject FindConstructor(int constructor) => FindConstructor((object)constructor);
        
        /// <summary>
        /// Returns a compiled skeleton of a specified TLObject parsed from the layer schema
        /// </summary>
        /// <param name="constructor"></param>
        private static JObject FindConstructor(string constructor) => FindConstructor((object)constructor);
        
        /// <summary>
        /// Returns a compiled skeleton of a specified TLObject parsed from the layer schema
        /// </summary>
        /// <param name="constructor"></param>
        private static JObject FindConstructor(uint constructor) => FindConstructor((object)constructor);
        
        /// <summary>
        /// Returns a compiled skeleton of a specified TLObject parsed from the layer schema
        /// </summary>
        /// <param name="constructor"></param>
        private static JObject FindConstructor(object constructor)
        {
            var schema = TLSchema.Schema;

            if (constructor is uint u) constructor = (int)u;

            // Loop through the schema
            foreach (var (key, _) in schema)
            {
                // Skip the info
                if (key == "schema_info") continue;

                // Loop through each section...
                foreach (var jToken in schema[key])
                {
                    var tl = (JObject) jToken;
                    
                    // Compare the entry according to the type of the Constructor

                    if (string.Equals(tl["name"]?.ToString(), constructor.ToString(), StringComparison.CurrentCultureIgnoreCase)) return tl;
                    if (string.Equals(Convert.ToInt32(tl["hexid"]?.ToString(), 16).ToString(), constructor.ToString(), StringComparison.CurrentCultureIgnoreCase)) return tl;
                    if (string.Equals(tl["id"]?.ToString(), constructor.ToString(), StringComparison.CurrentCultureIgnoreCase)) return tl;
                }
            }

            // If nothing is found, return null
            return null;
        }
    }
}
