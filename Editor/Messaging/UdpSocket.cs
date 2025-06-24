/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
using System;
using System.Net;
using System.Net.Sockets;

namespace Microsoft.Unity.IdeLinux.Editor.Messaging
{
	internal class UdpSocket : Socket
	{
		// Maximum UDP payload is 65507 bytes.
		// TCP mode will be used when the payload is bigger than this BufferSize
		public const int BufferSize = 1024 * 8;

		internal UdpSocket()
			: base(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
		{
			SetIOControl();
		}

		public void Bind(IPAddress address, int port = 0)
		{
			Bind(new IPEndPoint(address ?? IPAddress.Any, port));
		}

		private void SetIOControl()
		{
			// Note: Removed Windows-specific SIO_UDP_CONNRESET handling
			// Linux UDP socket handling is sufficient for our needs
		}

		public static byte[] BufferFor(IAsyncResult result)
		{
			return (byte[])result.AsyncState;
		}

		public static EndPoint Any()
		{
			return new IPEndPoint(IPAddress.Any, 0);
		}
	}
}
