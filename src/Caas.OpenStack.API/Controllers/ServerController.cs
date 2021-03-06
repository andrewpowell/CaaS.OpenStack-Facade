﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright company="" file="ServerController.cs">
//   
// </copyright>
// <author>Anthony.Shaw@dimensiondata.com</author>
// <date>4/13/2015</date>
// <summary>
//   API Actions for server management
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web.Http;
using Caas.OpenStack.API.Exceptions;
using Caas.OpenStack.API.Interfaces;
using Caas.OpenStack.API.Models;
using Caas.OpenStack.API.Models.api;
using Caas.OpenStack.API.Translators;
using Caas.OpenStack.API.UriFactories;
using DD.CBU.Compute.Api.Client.Interfaces;
using DD.CBU.Compute.Api.Contracts.General;
using DD.CBU.Compute.Api.Contracts.Image;
using DD.CBU.Compute.Api.Contracts.Server;

namespace Caas.OpenStack.API.Controllers
{
	using Models.server;

	/// <summary>	A controller for handling servers. </summary>
	/// <remarks>	Anthony, 4/13/2015. </remarks>
	/// <seealso cref="T:System.Web.Http.ApiController"/>
	/// <seealso cref="T:Caas.OpenStack.API.Interfaces.IOpenStackApiServerController"/>
	[Authorize]
	[RoutePrefix(Constants.ServerPrefix)]
	public class ServerController : ApiController, IOpenStackApiServerController
	{
		/// <summary>	The compute client. </summary>
		private readonly IComputeApiClient _computeClient;

		/// <summary>
		/// Initialises a new instance of the <see cref="ServerController"/> class. 
		/// Initializes a new instance of the <see cref="ServerController"/> class.
		/// </summary>
		/// <param name="apiClient">
		/// The API client.
		/// </param>
        public ServerController(Func<Uri, IComputeApiClient> apiClient)
        {
            _computeClient = apiClient(ConfigurationHelpers.GetApiUri());
        }

		/// <summary>
		/// 	Gets the limits. OpenStack equivalent GET/v2/​{tenant_id}​/limits. 
		/// </summary>
		/// <remarks>
		/// 	Anthony, 4/13/2015. 
		/// </remarks>
		/// <param name="tenantId">
		/// 	Identifier for the tenant. 
		/// </param>
		/// <returns>
		/// 	The limits. 
		/// </returns>
		/// <seealso cref="M:Caas.OpenStack.API.Interfaces.IOpenStackApiServerController.GetLimits(string)"/>
		[HttpGet]
		[Route("{tenantId}/limits")]
		public Task<LimitsResponse> GetLimits(string tenantId)
		{
			// TODO : Call the compute APIs to match up the limit specifications in OpenStack.
			return Task.FromResult(new LimitsResponse
			{
				Limits = new Limits()
			});
		}

		/// <summary>	(An Action that handles HTTP GET requests) gets server list head. </summary>
		/// <remarks>	Anthony, 4/20/2015. </remarks>
		/// <param name="tenantId">	The tenantId. </param>
		/// <returns>	The server list. </returns>
		[Route("{tenantId}")]
		[HttpHead]
		public async Task<BaseServerResponse> GetServerListHead([FromUri] string tenantId)
		{
			ServerWithBackupType[] remoteServerCollection = (await _computeClient.GetDeployedServers()).ToArray();
			List<BaseServer> servers = new List<BaseServer>();

			for (int i = 0; i < servers.Count(); i++)
			{
				servers.Add(Request.CaaSServerToServer(remoteServerCollection[i], tenantId));
			}

			return new BaseServerResponse
			{
				Servers = servers.ToArray()
			};
		}

		/// <summary>
		/// 	(An Action that handles HTTP GET requests) gets server list. 
		/// </summary>
		/// <remarks>
		/// 	Anthony, 4/13/2015. 
		/// </remarks>
		/// <param name="tenantId">
		/// 	The tenantId. 
		/// </param>
		/// <returns>
		/// 	The server list. 
		/// </returns>
		[Route("{tenantId}/servers")]
		[HttpGet]
		public async Task<BaseServerResponse> GetServerList([FromUri] string tenantId)
		{
			ServerWithBackupType[] remoteServerCollection = (await _computeClient.GetDeployedServers()).ToArray();
			List<BaseServer> servers = new List<BaseServer>();

			for (int i = 0; i < servers.Count(); i++)
			{
				servers.Add(Request.CaaSServerToServer(remoteServerCollection[i], tenantId));
			}

			return new BaseServerResponse
			{
				Servers = servers.ToArray()
			};
		}

		/// <summary>
		/// Creates a server. OpenStack equivalent: POST/v2/​{tenantId}​/servers -&gt; "Create
		/// 	Server". 
		/// </summary>
		/// <remarks>
		/// 	Anthony, 4/13/2015. 
		/// </remarks>
		/// <param name="tenantId">
		/// 	Identifier for the tenant. 
		/// </param>
		/// <param name="request">
		/// 	The request. 
		/// </param>
		/// <returns>
		/// 	The new server. 
		/// </returns>
		/// <seealso cref="M:Caas.OpenStack.API.Interfaces.IOpenStackApiServerController.CreateServer(string,ServerProvisioningRequest)"/>
		/// <seealso cref="M:Caas.OpenStack.API.Interfaces.IOpenStackApiServerController.CreateServer(ServerProvisioningRequest)"/>
		[HttpPost]
		[Route("{tenantId}​/servers")]
		public async Task<ServerProvisioningResponse> CreateServer(string tenantId, [FromBody] ServerProvisioningRequest request)
		{
			// Get the image
			ImagesWithDiskSpeedImage imageResult = (await _computeClient.GetImages(request.Server.ImageRef, string.Empty, string.Empty, string.Empty, string.Empty)).FirstOrDefault();

			if (imageResult == null)
				throw new ImageNotFoundException();

			// Generate secret.
			byte[] buffer = new byte[9];
			using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
				rng.GetBytes(buffer);
			string adminPass = Convert.ToBase64String(buffer).Substring(0, 10).Replace('/', '0').Replace('+', '1');

			// Provision a server.
			Status status = await _computeClient.DeployServerImageTask(
				request.Server.Name, 
				string.Empty, 
				request.Server.Networks.First().Uuid, // NB: Support for multiple networks in MCP2.0
				request.Server.ImageRef, 
				adminPass, 
				true);

			if (status.result == "SUCCESS")
			{
				string newServerId = status.additionalInformation.First(item => item.name == "serverId").value;
				return new ServerProvisioningResponse
				{
					Server = new ServerProvisioningResponseServer
					{
						Id = newServerId, // This is the server code.
						AdminPass = adminPass, 
						Links = new[]
						{
							new RestLink(ServerUriFactory.GetServerUri(Request.RequestUri.Host, tenantId, newServerId), RestLink.Self)
						}
					}
				};
			}

			throw new ServerProvisioningRequestFailedException();
		}

		/// <summary>
		/// Gets the server detail list for a given tenant.
		/// </summary>
		/// <remarks>
		/// 	Anthony, 4/13/2015. 
		/// </remarks>
		/// <param name="tenantId">
		/// 	The tenantId. 
		/// </param>
		/// <returns>
		/// 	The server detail list. 
		/// </returns>
		/// <seealso cref="M:Caas.OpenStack.API.Interfaces.IOpenStackApiServerController.GetServerDetailList(string)"/>
		[Route("{tenantId}/servers/detail")]
        [HttpGet]
		public async Task<ServerDetailList> GetServerDetailList([FromUri] string tenantId)
		{
			ServerWithBackupType[] servers = (await _computeClient.GetDeployedServers()).ToArray();
			ServerDetailList serverList = new ServerDetailList
			{
				Servers = new ServerDetail[servers.Count()]
			};

			for (int i = 0; i < servers.Count(); i++)
			{
				serverList.Servers[i] = Request.CaaSServerToServerDetail(servers[i], tenantId);
			}

			return serverList;
		}

		/// <summary>
		/// 	Gets details about a particular server. 
		/// </summary>
		/// <remarks>
		/// 	Anthony, 4/13/2015. 
		/// </remarks>
		/// <param name="tenantId">
		/// 	The tenantId. 
		/// </param>
		/// <param name="serverId">
		/// 	The serverId. 
		/// </param>
		/// <returns>
		/// 	The server detail. 
		/// </returns>
		/// <seealso cref="M:Caas.OpenStack.API.Interfaces.IOpenStackApiServerController.GetServerDetail(string,string)"/>
		[Route("{tenantId}/servers/{serverId}")]
		[HttpGet]
		public async Task<ServerDetailResponse> GetServerDetail([FromUri]string tenantId, [FromUri]string serverId)
		{
			ServerWithBackupType caasServer = (await _computeClient.GetDeployedServers()).First(server => server.id == serverId);
			return
				new ServerDetailResponse
				{
					Server = Request.CaaSServerToServerDetail(caasServer, tenantId)
				};
		}

		/// <summary>
		/// 	Updates the server. PUT/v2/​{tenant_id}​/servers/​{server_id}​. 
		/// </summary>
		/// <remarks>
		/// 	Anthony, 4/13/2015. 
		/// </remarks>
		/// <param name="tenantId">
		/// 			  	Identifier for the tenant. 
		/// </param>
		/// <param name="serverId">
		/// 			  	Identifier for the server. 
		/// </param>
		/// <param name="updateServerRequest">
		/// 	The update server request. 
		/// </param>
		/// <returns>
		/// 	The new server; 
		/// </returns>
		/// <seealso cref="M:Caas.OpenStack.API.Interfaces.IOpenStackApiServerController.UpdateServer(string,string,dynamic)"/>
		[Route("​{tenantId}​/servers/​{serverId}​")]
		[HttpPut]
		public Task<ServerDetailResponse> UpdateServer(string tenantId, string serverId, dynamic updateServerRequest)
		{
			// TODO : Process update fields.
			return GetServerDetail(tenantId, serverId);
		}

		/// <summary>
		/// Deletes the server. DELETE/v2/​{tenant_id}​/servers/​{server_id}​ -&gt; Remove Server. 
		/// </summary>
		/// <remarks>
		/// 	Anthony, 4/13/2015. 
		/// </remarks>
		/// <param name="tenantId">
		/// 	Identifier for the tenant. 
		/// </param>
		/// <param name="serverId">
		/// 	Identifier for the server. 
		/// </param>
		/// <returns>
		/// 	A Task. 
		/// </returns>
		/// <seealso cref="M:Caas.OpenStack.API.Interfaces.IOpenStackApiServerController.DeleteServer(string,string)"/>
		[Route("{tenantId}​/servers/​{serverId}")]
		[HttpDelete]
		public async Task DeleteServer(string tenantId, string serverId)
		{
			await _computeClient.ServerDelete(serverId);
		}

		/// <summary>
		/// 	List extensions OpenStack equivalent- &gt; GET/v2/​{tenant_id}​/extensions. 
		/// </summary>
		/// <remarks>
		/// 	Anthony, 4/13/2015. 
		/// </remarks>
		/// <param name="tenantId">
		/// 	Identifier for the tenant. 
		/// </param>
		/// <returns>
		/// 	A list of. 
		/// </returns>
		/// <seealso cref="M:Caas.OpenStack.API.Interfaces.IOpenStackApiServerController.ListExtensions(string)"/>
		[HttpGet]
		[Route("{tenantId}/extensions")]
		public Task<ExtensionCollectionResponse> ListExtensions(string tenantId)
		{
			// No extensions are supported.
			return Task.FromResult(new ExtensionCollectionResponse
			{
				Extensions = new[]
				{
					Extension.KeyPairExtension
				}
			});
		}
	}
}