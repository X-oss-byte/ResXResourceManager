﻿namespace tomenglertde.ResXManager.Translators
{
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics.Contracts;
    using System.Threading;

    using Newtonsoft.Json;

    using tomenglertde.ResXManager.Translators.Properties;

    public static class TranslatorHost
    {
        public static readonly ITranslator[] Translators = 
        {
            new BingTranslator(),
            // new GoogleTranslator(), 
            new MyMemoryTranslator(),
        };

        static TranslatorHost()
        {
            var settings = Settings.Default;
            var configuration = settings.Configuration;

            if (string.IsNullOrEmpty(configuration))
                return;

            try
            {
                var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(configuration);
                Contract.Assume(values != null);

                foreach (var translator in Translators)
                {
                    Contract.Assert(translator != null);

                    string setting;

                    if (!values.TryGetValue(translator.Id, out setting))
                        continue;
                    if (string.IsNullOrEmpty(setting))
                        continue;

                    try
                    {
                        JsonConvert.PopulateObject(setting, translator);
                    }
                    catch // Newtonsoft.Jason has not documented any exceptions...
                    {
                    }
                }
            }
            catch // Newtonsoft.Jason has not documented any exceptions...
            {
            }
        }

        public static void SaveConfiguration()
        {
            var settings = Settings.Default;

            var values = new Dictionary<string, string>();

            foreach (var translator in Translators)
            {
                Contract.Assume(translator != null);

                var json = JsonConvert.SerializeObject(translator);
                values[translator.Id] = json;
            }

            settings.Configuration = JsonConvert.SerializeObject(values);
        }

        public static void Translate(Session session)
        {
            Contract.Requires(session != null);

            var translatorCounter = 0;

            foreach (var translator in Translators)
            {
                Contract.Assume(translator != null);

                var local = translator;
                if (!local.IsEnabled)
                    continue;

                Interlocked.Increment(ref translatorCounter);

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        local.Translate(session);
                    }
                    finally
                    {
                        if (Interlocked.Decrement(ref translatorCounter) == 0)
                        {
                            session.IsComplete = true;
                        }
                    }
                });
            }

            if (translatorCounter == 0)
            {
                session.IsComplete = true;
            }
        }
    }
}