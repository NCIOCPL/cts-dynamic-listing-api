using System;
using System.Linq;
using System.Collections.Generic;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Nest;

using System.Threading.Tasks;
using NCI.OCPL.Api.Common;
using NCI.OCPL.Api.CTSListingPages.Models;

namespace NCI.OCPL.Api.CTSListingPages.Services
{
    /// <summary>
    /// Elasticsearch implemenation of the service for retrieving Listing Info documents.
    /// </summary>
    public class ESListingInfoQueryService : IListingInfoQueryService
    {
        /// <summary>
        /// The elasticsearch client
        /// </summary>
        private IElasticClient _elasticClient;

        /// <summary>
        /// The API options.
        /// </summary>
        protected readonly ListingPageAPIOptions _apiOptions;

        /// <summary>
        /// A logger to use for logging
        /// </summary>
        private readonly ILogger<ESListingInfoQueryService> _logger;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ESListingInfoQueryService(IElasticClient client, IOptions<ListingPageAPIOptions> apiOptionsAccessor,
            ILogger<ESListingInfoQueryService> logger)
        {
            _elasticClient = client;
            _apiOptions = apiOptionsAccessor.Value;
            _logger = logger;
        }

        /// <summary>
        /// Retrieve the name and URL data for an EVS concept with a pretty-url name exactly matching the name parameter.
        /// </summary>
        /// <param name="prettyUrlName">The pretty-url name of the record to be retrieved.</param>
        /// <returns>A ListingInfo object or null if an exact match is not found.</returns>
        public async Task<ListingInfo> GetByPrettyUrlName(string prettyUrlName)
        {
            // Set up the SearchRequest to send to elasticsearch.
            Indices index = Indices.Index(new string[] { this._apiOptions.ListingInfoAliasName });
            SearchRequest request = new SearchRequest(index)
            {
                Query = new TermQuery { Field = "pretty_url_name", Value = prettyUrlName.ToString() }
            };

            ISearchResponse<ListingInfo> response = null;
            try
            {
                response = await _elasticClient.SearchAsync<ListingInfo>(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching index: '{this._apiOptions.ListingInfoAliasName}'.");
                throw new APIInternalException("errors occured");
            }

            if (!response.IsValid)
            {
                String msg = $"Invalid response when searching for pretty URL name '{prettyUrlName}'.";
                _logger.LogError(msg);
                _logger.LogError(response.DebugInformation);
                throw new APIInternalException("errors occured");
            }

            ListingInfo record = null;

            // If there is are any records in the response, the lookup was successful.
            if (response.Total > 0)
            {
                record = response.Documents.First();

                if (response.Total > 1)
                {
                    _logger.LogWarning($"Found multiple records for pretty URL name '{prettyUrlName}'.");
                }
            }

            return record;
        }

        /// <summary>
        /// Retrieve the name and URL data for EVS concept(s) with a c-code (list) exactly or partially matching the name parameter.
        /// </summary>
        /// <param name="ccodes">The c-code list of the record to be retrieved.</param>
        /// <returns>An array of ListingInfo objects or null if exact or partial matches are not found.</returns>
        public async Task<ListingInfo[]> GetByIds(string[] ccodes)
        {
            // Set up the SearchRequest to send to elasticsearch.
            Indices index = Indices.Index(new string[] { this._apiOptions.ListingInfoAliasName });
            SearchRequest request = new SearchRequest(index)
            {
                Query = new TermsSetQuery {
                    Field = "concept_id",
                    Terms = ccodes,
                    MinimumShouldMatchScript = new InlineScript("params.num_terms")
                }
            };

            ISearchResponse<ListingInfo> response = null;
            try
            {
                response = await _elasticClient.SearchAsync<ListingInfo>(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching index: '{this._apiOptions.ListingInfoAliasName}'.");
                throw new APIInternalException("errors occured");
            }

            if (!response.IsValid)
            {
                String msg = $"Invalid response when searching for c-code(s) '{String.Join(",", ccodes)}'.";
                _logger.LogError(msg);
                _logger.LogError(response.DebugInformation);
                throw new APIInternalException("errors occured");
            }

            ListingInfo[] results = null;

            if(response.Documents.Count > 0)
            {
                results = response.Documents.ToArray();
            }

            return results;
        }
    }
}
