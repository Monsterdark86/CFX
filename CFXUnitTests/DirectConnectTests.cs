﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Cryptography.X509Certificates;
using CFX;
using CFX.Transport;

namespace CFXUnitTests
{
    [TestClass]
    public class DirectConnectTests
    {
        [TestMethod]
        public void NoAuthNoSec()
        {
            DoTests(false, false);
        }

        [TestMethod]
        public void AuthNoSec()
        {
            DoTests(true, false);
        }

        [TestMethod]
        public void NoAuthSec()
        {
            DoTests(false, true);
        }

        [TestMethod]
        public void AuthAndSec()
        {
            DoTests(true, true);
        }

        private void DoTests(bool auth, bool sec)
        {
            InitializeTest(auth, sec);

            // Publish basic EndpointConnected message and ensure receipt by listener
            EndpointConnected msg = new EndpointConnected()
            {
                CFXHandle = TestSettings.ClientHandle
            };

            FireAndWait(msg);

            // Send Request/Reponse pattern command, and ensure response
            CFXEnvelope req = new CFXEnvelope(new AreYouThereRequest() { CFXHandle = listener.CFXHandle });
            req.Source = endpoint.CFXHandle;
            req.Target = listener.CFXHandle;
            CFXEnvelope resp = endpoint.ExecuteRequest(listener.RequestUri.ToString(), req);
            if (resp == null) throw new Exception("Invalid response to command request");
        }

        private AmqpCFXEndpoint endpoint = null;
        private AmqpCFXEndpoint listener = null;

        private void InitializeTest(bool auth, bool sec)
        {
            SetupListener(auth, sec);
            SetupEndpoint(auth, sec);
        }

        private void SetupListener(bool auth, bool sec)
        {
            KillListener();

            listener = new AmqpCFXEndpoint();
            listener.Open(TestSettings.ListenerHandle, TestSettings.GetListenerUri(auth, sec), certificate: TestSettings.GetCertificate(sec));
            listener.OnCFXMessageReceivedFromListener += Listener_OnCFXMessageReceivedFromListener;
            listener.OnRequestReceived += Listener_OnRequestReceived;

            listener.AddListener(TestSettings.ListenerAddress);
        }

        private void SetupEndpoint(bool auth, bool sec)
        {
            KillEndpoint();

            endpoint = new AmqpCFXEndpoint();
            endpoint.Open(TestSettings.ClientHandle, certificate: TestSettings.GetCertificate(sec));
            endpoint.ValidateCertificates = false;

            Exception ex = null;
            Uri uri = TestSettings.GetListenerUri(auth, sec);
            if (!endpoint.TestPublishChannel(uri, TestSettings.ListenerAddress, out ex))
            {
                throw new Exception($"Cannot connect to listener at {uri.ToString()}:  {ex.Message}", ex);
            }

            endpoint.AddPublishChannel(uri, TestSettings.ListenerAddress);
        }

        private System.Threading.AutoResetEvent evt;

        private void FireAndWait(CFXMessage msg)
        {
            using (evt = new System.Threading.AutoResetEvent(false))
            {
                endpoint.Publish(msg);
                if (!evt.WaitOne(1000))
                {
                    throw new TimeoutException("The message was not received by listener.  Timeout");
                }
            }
        }

        private CFXEnvelope Listener_OnRequestReceived(CFXEnvelope request)
        {
            if (request.MessageBody is AreYouThereRequest)
            {
                CFXEnvelope resp = new CFXEnvelope(new AreYouThereResponse() { CFXHandle = listener.CFXHandle, RequestNetworkUri = listener.RequestUri.ToString(), RequestTargetAddress = null });
                return resp;
            }

            return null;
        }

        private void Listener_OnCFXMessageReceivedFromListener(string targetAddress, CFXEnvelope message)
        {
            if (evt != null) evt.Set();
        }

        [TestCleanup]
        public void KillAll()
        {
            KillEndpoint();
            KillListener();
        }

        private void KillListener()
        {
            if (listener != null)
            {
                listener.Close();
                listener = null;
            }
        }

        private void KillEndpoint()
        {
            if (endpoint != null)
            {
                endpoint.Close();
                endpoint = null;
            }
        }
    }
}