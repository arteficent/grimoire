using grimoire.Model;
using static grimoire.Model.Tone;

namespace grimoire.Stories;

internal static class InterfaceLessons
{
    public static IEnumerable<Lesson> All()
    {
        yield return Layers();
        yield return Ethernet();
        yield return IpRouting();
        yield return Tcp();
        yield return UdpQuic();
        yield return WiFi();
        yield return Usb();
        yield return UsbC();
        yield return Bluetooth();
        yield return Pcie();
        yield return Display();
        yield return EmbeddedBuses();
        yield return IotAndCellular();
        yield return Atlas();
    }

    private static string Row(string name, string latest, string next) => $"{name,-14}{latest,-38}{next}";

    // ------------------------------------------------------------------------------------------
    private static Lesson Layers()
    {
        string[] osi =
        [
            "7 Application   HTTP, DNS", "6 Presentation  TLS", "5 Session", "4 Transport     TCP, UDP, QUIC",
            "3 Network       IP", "2 Data link     Ethernet, Wi-Fi", "1 Physical      copper, fibre, radio"
        ];

        void Osi(SceneState s, params int[] hot) => s.Table("osi", "OSI model (TCP/IP merges 5-7)", 30, 40, 260, osi).HotRow("osi", hot);

        return new StoryBuilder("IO1", "Every network message is wrapped layer by layer: each layer adds its own header in front of the data from the layer above, and only reads its own header at the other end. That is what lets HTTP run unchanged over Ethernet, Wi-Fi or 5G.")
            .Setup(s => Osi(s))
            .Code("""
                await http.GetAsync("https://example.com/index.html");
                // HTTP     -> "GET /index.html"
                // TLS      -> encrypt; 5 B record header + 16 B auth tag
                // TCP      -> 20 B header: ports, sequence, ack, window
                // IP       -> 20 B header: source/destination address, TTL
                // Ethernet -> 14 B header: MAC addresses, EtherType; 4 B CRC trailer
                """)
            .Step("Application data",
                "The browser produces an HTTP request: GET /index.html. That is all the application cares about.",
                "The OSI model has 7 layers. The internet's TCP/IP model merges the top three into one 'application' layer. Engineers still say 'L2', 'L3', 'L4' and 'L7' using OSI numbering.",
                [1, 2],
                s =>
                {
                    Osi(s, 0);
                    s.Fields("app", [("HTTP request", "GET /index.html", 1)], 480, 40, 300, 60, Good);
                })
            .Step("TLS encrypts it",
                "TLS encrypts the request and frames it as a record: a 5-byte header, the ciphertext, and a 16-byte authentication tag.",
                "Everything below this point sees only encrypted bytes. TLS 1.3 (2018) needs a single round trip to set up, and QUIC builds it in.",
                [3],
                s =>
                {
                    Osi(s, 1);
                    s.Fields("tls", [("TLS hdr", "5 B", 1), ("encrypted HTTP", "a7 3f 91 ...", 3), ("tag", "16 B", 1)], 420, 130, 420, 60, Warn);
                })
            .Step("TCP adds ports and sequence numbers",
                "The transport layer adds a 20-byte header: source and destination ports (443 for HTTPS), sequence and acknowledgement numbers, and the receive window.",
                "Ports identify the application on each host. Sequence numbers let TCP reorder data and retransmit losses (see the TCP lesson).",
                [4],
                s =>
                {
                    Osi(s, 3);
                    s.Fields("tcp", [("TCP header", "20 B", 1), ("TLS record (payload)", "...", 4)], 370, 220, 520, 60, Info);
                })
            .Step("IP adds addresses",
                "The network layer adds a 20-byte IPv4 header (40 bytes for IPv6): source and destination IP addresses, a TTL hop counter and the protocol number (6 = TCP).",
                "Routers along the path read only this header to forward the packet. They never need to look at TCP or HTTP.",
                [5],
                s =>
                {
                    Osi(s, 4);
                    s.Fields("ip", [("IP header", "20 B", 1), ("TCP segment", "...", 4)], 330, 310, 600, 60);
                    s.Tint("ip0", 3);
                })
            .Step("Ethernet frames it for the wire",
                "The link layer adds a 14-byte header (destination and source MAC address, EtherType 0x0800 = IPv4) and a 4-byte CRC trailer, then the physical layer turns it into signals.",
                "The link-layer header is rewritten at every router hop, while the IP header stays the same end to end (apart from TTL). A 1500-byte payload limit (the MTU) is why large transfers are split into many packets.",
                [6],
                s =>
                {
                    Osi(s, 5, 6);
                    s.Fields("eth", [("Ethernet", "14 B", 1), ("IP packet (up to 1500 B)", "...", 4), ("FCS", "4 B", 0.8)], 300, 400, 660, 60);
                    s.Tint("eth0", 1).Tint("eth2", 1);
                })
            .Step("Unwrapped in reverse",
                "The receiver peels the layers off in reverse: the NIC checks the CRC, IP checks the address, TCP reorders and acknowledges, TLS decrypts, and HTTP sees the request.",
                "Of a 1514-byte frame, about 54 bytes are Ethernet + IP + TCP headers (plus TLS overhead). That is about 4% overhead for full-size packets, but huge for tiny ones, which is why batching small messages matters.",
                [],
                s =>
                {
                    Osi(s, 0, 1, 2, 3, 4, 5, 6);
                    s.Say("Each layer adds a header on the way down and strips it on the way up", Good);
                })
            .Lesson("Network layers and encapsulation", "Networking", "OSI vs TCP/IP, and how HTTP gets wrapped in TLS, TCP, IP and Ethernet headers.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Ethernet()
    {
        const string MacA = "02:00:00:00:00:0A";
        const string MacB = "02:00:00:00:00:0B";

        return new StoryBuilder("IO2", "Ethernet is the wired LAN standard: framed packets with MAC addresses, sent through switches that learn which port each address lives on. Its speeds have grown 160,000x since 1983.")
            .Setup(s =>
            {
                s.Fields("fr", [("preamble+SFD", "8 B", 1.3), ("dst MAC", "6 B", 1.6), ("src MAC", "6 B", 1.6), ("802.1Q VLAN", "4 B", 1.2), ("EtherType", "2 B", 1.1), ("payload", "46-1500 B", 3.4), ("FCS CRC-32", "4 B", 1.3)], 40, 30, 920, 64);
                s.Box("sw", "switch", 380, 200, 240, 64, "learns MAC -> port");
                s.Table("mac", "MAC table", 680, 150, 290, "(empty)");
                string[] names = ["A", "B", "C", "D"];
                for (var i = 0; i < 4; i++)
                {
                    s.Box("h" + i, "host " + names[i], 40 + i * 245, 400, 180, 60, $"...:0{(char)('A' + i)}");
                    s.Connect("sw", "h" + i, $"port {i + 1}", arrow: false);
                }
            })
            .NoCode()
            .Step("Anatomy of a frame",
                "A preamble for clock sync, destination and source MAC addresses, an optional VLAN tag, an EtherType saying what is inside (0x0800 IPv4, 0x86DD IPv6, 0x0806 ARP), 46-1500 bytes of payload, and a CRC-32.",
                "A MAC address is 48 bits written as 6 hex bytes. The first 3 bytes usually identify the vendor, and FF:FF:FF:FF:FF:FF is broadcast. 'Jumbo frames' (about 9000 bytes) are a common non-standard extension in data centres.",
                s => s.PaintAll(Info, "fr1", "fr2", "fr4"))
            .Step("Unknown destination: flood",
                "Host A sends a frame to B. The switch notes that A's MAC arrived on port 1. It does not know B yet, so it floods the frame out of every other port.",
                "Flooding is the switch's fallback. Hosts C and D discard the frame because the destination MAC is not theirs.",
                s =>
                {
                    s.PaintAll(Neutral, "fr1", "fr2", "fr4");
                    s.Move("shape:h0", "shape:sw", "dst " + MacB, Info);
                    s.Move("shape:sw", "shape:h1", "flood", Warn).Move("shape:sw", "shape:h2", "flood", Warn).Move("shape:sw", "shape:h3", "flood", Warn);
                    s.Table("mac", "MAC table", 680, 150, 290, MacA + "  port 1").HotRow("mac", 0);
                    s.Paint("h0", Info);
                })
            .Step("Reply: learned and forwarded",
                "B replies. The switch learns B is on port 2 and already knows A is on port 1, so it forwards the frame only to port 1.",
                "Every port is its own collision domain and runs full duplex, so modern switched Ethernet has no collisions. The 1980s shared-cable CSMA/CD only survives in the standard's history.",
                s =>
                {
                    s.Paint("h0", Neutral).Paint("h1", Info);
                    s.Move("shape:h1", "shape:sw", "dst " + MacA, Info).Move("shape:sw", "shape:h0", "forward", Good);
                    s.Table("mac", "MAC table", 680, 150, 290, MacA + "  port 1", MacB + "  port 2").HotRow("mac", 1);
                })
            .Step("40 years of speed",
                "From 10 Mb/s over coaxial cable to 800 Gb/s over fibre, each step reusing the same frame format.",
                "Copper twisted pair reaches 10 Gb/s (10GBASE-T). Beyond that, data centres use fibre or short copper cables driven by PAM4 SerDes lanes running at 100-200 Gb/s each.",
                s =>
                {
                    s.Paint("h1", Neutral);
                    s.RemoveWhere(sh => sh.Id is "sw" or "mac" || sh.Id.StartsWith('h'));
                    s.LabeledBars("sp", [("1983", 10, "10M"), ("1995", 100, "100M"), ("1998", 1e3, "1G"), ("2002", 1e4, "10G"), ("2010", 1e5, "100G"), ("2017", 4e5, "400G"), ("2024", 8e5, "800G"), ("in dev", 1.6e6, "1.6T")], 40, 500, 100, 17, 340, log: true);
                })
            .Step("Today and next",
                "800 Gb/s Ethernet (IEEE 802.3df) was approved in February 2024. 1.6 Tb/s (802.3dj, 200 Gb/s per lane) is in its final ballots. Ultra Ethernet (UEC 1.0, 2025) tunes Ethernet transport for AI and HPC clusters.",
                "AI training clusters drive this: thousands of GPUs exchanging gradients need high bandwidth plus low tail latency. Ultra Ethernet adds multipath packet spraying and new congestion control on top of standard Ethernet.",
                s =>
                {
                    s.Paint("sp7", Warn).Ghost("sp7").Paint("sp6", Good);
                    s.Say("Same frame format from 10 Mb/s to 1.6 Tb/s", Info);
                })
            .Lesson("Ethernet: frames, switches and speeds", "Networking", "Frame layout, MAC learning in switches, and Ethernet from 10 Mb/s to 1.6 Tb/s.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson IpRouting() =>
        new StoryBuilder("IO3", "IP gets packets across networks. Hosts use ARP to find MAC addresses on their own subnet, send everything else to a gateway, and routers forward hop by hop using the longest matching prefix.")
            .Setup(s =>
            {
                s.Fields("pk", [("dst MAC", "?", 2), ("src MAC", "A", 1.5), ("src IP", "192.168.1.10", 3), ("dst IP", "?", 3), ("TTL", "64", 1)], 30, 30, 940, 60);
                s.Region("lan", "home LAN 192.168.1.0/24", 30, 140, 400, 300, Info);
                s.Box("ha", "host A", 60, 190, 160, 60, "192.168.1.10");
                s.Box("hb", "host B", 60, 350, 160, 60, "192.168.1.20");
                s.Box("sw", "switch", 280, 270, 110, 50);
                s.Box("rt", "home router", 460, 260, 160, 70, "192.168.1.1 | 203.0.113.7");
                s.Region("net", "internet", 650, 140, 320, 300, Warn);
                s.Box("r2", "router R2", 680, 190, 120, 50);
                s.Box("r3", "router R3", 680, 350, 120, 50);
                s.Box("srv", "server", 830, 270, 130, 60, "198.51.100.20");
                s.Connect("ha", "sw", arrow: false).Connect("hb", "sw", arrow: false).Connect("sw", "rt", arrow: false)
                    .Connect("rt", "r2", arrow: false).Connect("rt", "r3", arrow: false).Connect("r2", "srv", arrow: false).Connect("r3", "srv", arrow: false);
            })
            .NoCode()
            .Step("Same subnet: ARP first",
                "A wants to reach 192.168.1.20. The /24 mask says it is on the same subnet, but A needs B's MAC address. It broadcasts an ARP request: 'who has 192.168.1.20?'. B answers with its MAC.",
                "The answer is cached (arp -a shows the cache). IPv6 replaces ARP with Neighbor Discovery over ICMPv6. ARP has no authentication, which is what 'ARP spoofing' attacks exploit.",
                s =>
                {
                    s.Write("pk0", "FF:FF:..").Write("pk3", "192.168.1.20");
                    s.Paint("pk0", Warn);
                    s.Move("shape:ha", "shape:sw", "ARP who has .20?", Warn).Move("shape:sw", "shape:hb", "broadcast", Warn).Move("shape:sw", "shape:rt", "broadcast", Warn);
                    s.Move("shape:hb", "shape:ha", "ARP reply: my MAC", Good);
                })
            .Step("Another network: send to the gateway",
                "Now A sends to the server 198.51.100.20, which is outside 192.168.1.0/24. A puts the router's MAC in the Ethernet header but keeps the server's IP in the IP header.",
                "This is the key split: MAC addresses get the frame across one link, while IP addresses identify the final destination. The 'default gateway' setting is how a host knows which router to use.",
                s =>
                {
                    s.Write("pk0", "router").Write("pk3", "198.51.100.20");
                    s.Paint("pk0", Info).Paint("pk3", Info);
                    s.Paint("ha", Info);
                    s.Move("shape:ha", "shape:rt", "to gateway MAC", Info);
                })
            .Step("NAT at the home router",
                "Home networks use private addresses (192.168.x.x). The router rewrites the source to its single public address (203.0.113.7) and a new port, and remembers the mapping to translate replies back.",
                "NAT extended IPv4's life (only about 4.3 billion addresses) but breaks end-to-end connectivity. Peer-to-peer apps need hole punching or relays (STUN/TURN).",
                s =>
                {
                    s.PaintAll(Neutral, "pk0", "pk3", "ha");
                    s.Write("pk2", "203.0.113.7").Paint("pk2", Warn);
                    s.Write("pk0", "ISP router").Write("pk1", "home rtr");
                    s.Paint("rt", Warn);
                })
            .Step("Routing: longest prefix match",
                "Each router decrements TTL and looks up the destination in its routing table. The most specific matching prefix wins: 198.51.100.0/24 beats 198.51.0.0/16, which beats the default route 0.0.0.0/0.",
                "Routers do this lookup in hardware (TCAMs) at hundreds of millions of packets per second. BGP exchanges routes between networks: about a million IPv4 prefixes in the global table. TTL reaching 0 drops the packet, which is how traceroute works.",
                s =>
                {
                    s.Paint("rt", Neutral).Paint("pk2", Neutral);
                    s.Table("rtab", "router table", 420, 450, 220, "198.51.100.0/24 -> R3", "198.51.0.0/16   -> R2", "0.0.0.0/0       -> ISP").HotRow("rtab", 0);
                    s.Write("pk4", "63").Paint("pk4", Warn);
                    s.Paint("r3", Good).Paint("srv", Good);
                    s.Move("shape:rt", "shape:r3", "/24 match", Good).Move("shape:r3", "shape:srv", "deliver", Good);
                })
            .Step("IPv6",
                "IPv6 addresses are 128 bits, written as 8 groups of hex, for example 2001:db8::1 ('::' skips zeros). There are enough addresses for every device, so NAT is not needed.",
                "IPv6 has a fixed 40-byte header, no router fragmentation, and Neighbor Discovery instead of ARP. Close to half of Google's users now reach it over IPv6, and mobile networks are often IPv6-first.",
                s =>
                {
                    s.Remove("rtab");
                    s.PaintAll(Neutral, "pk4", "r3", "srv");
                    s.Fields("v6", [("IPv6 address: 128 bits = 8 x 16-bit hex groups", "2001:0db8:0000:0000:0000:0000:0000:0001  =  2001:db8::1", 1)], 30, 460, 940, 60, Info);
                })
            .Lesson("IP, ARP and routing", "Networking", "Subnets, ARP, gateways, NAT, longest-prefix routing and IPv6 addresses.");

    // ------------------------------------------------------------------------------------------
    private static Lesson Tcp() =>
        new StoryBuilder("IO4", "TCP turns unreliable packets into a reliable, ordered byte stream using a handshake, sequence numbers, acknowledgements, retransmission, flow control and congestion control.")
            .Setup(s => s.Lifelines("client", "server :443"))
            .Code("""
                using var client = new TcpClient();
                await client.ConnectAsync("example.com", 443);   // SYN, SYN-ACK, ACK
                var stream = client.GetStream();
                await stream.WriteAsync(data);                   // segments + ACKs
                client.Close();                                  // FIN, ACK ... TIME_WAIT
                """)
            .Step("Three-way handshake",
                "SYN (client's initial sequence number), SYN-ACK (server's sequence number plus acknowledgement), then ACK. Each side now knows the other's starting number.",
                "This costs one round trip (RTT) before any data, and TLS adds another. Initial sequence numbers are random to prevent spoofing. The server's backlog holds half-open connections, which is what a SYN flood attacks.",
                [2],
                s => s.Msg("msg1", true, 110, "SYN seq=1000").Msg("msg2", false, 190, "SYN-ACK seq=5000 ack=1001").Msg("msg3", true, 270, "ACK ack=5001", Good))
            .Step("Data and cumulative ACKs",
                "The client sends two 1460-byte segments without waiting. The server's single ACK 3921 means 'I have every byte before 3921', and its window says how much more it can buffer.",
                "1460 bytes is a typical payload size (MSS): 1500 MTU minus 40 bytes of IP and TCP headers. The receive window (flow control) stops a fast sender from overflowing a slow receiver's buffer.",
                [4],
                s =>
                {
                    s.ClearMessages();
                    s.Msg("msg1", true, 110, "seq=1001 len=1460").Msg("msg2", true, 170, "seq=2461 len=1460").Msg("msg3", false, 260, "ACK 3921, window 64 KB", Good);
                })
            .Step("Loss and fast retransmit",
                "Segment 3921 is lost. The following segment arrives, so the server keeps acknowledging 3921 (duplicate ACKs). After 3 duplicates, the client retransmits without waiting for a timeout.",
                "If ACKs stop entirely, a retransmission timeout (RTO, at least about 200 ms) fires instead, which is much slower. SACK (selective acknowledgement) tells the sender exactly which ranges arrived.",
                [4],
                s =>
                {
                    s.ClearMessages();
                    s.Msg("msg1", true, 100, "seq=3921", lost: true).Msg("msg2", true, 160, "seq=5381").Msg("msg3", false, 230, "ACK 3921 (dup x3)", Warn)
                        .Msg("msg4", true, 310, "seq=3921 retransmit", Warn).Msg("msg5", false, 390, "ACK 6841", Good);
                })
            .Step("Closing: FIN and TIME_WAIT",
                "Each direction closes separately: FIN, ACK, FIN, ACK. The side that closed first waits in TIME_WAIT (2 x maximum segment lifetime) so that stray old packets cannot confuse a new connection.",
                "Opening and closing many short connections leaves thousands of sockets in TIME_WAIT and can exhaust ports. This is why HttpClient should be reused, and why HTTP keep-alive and connection pooling exist.",
                [5],
                s =>
                {
                    s.ClearMessages();
                    s.Msg("msg1", true, 110, "FIN").Msg("msg2", false, 180, "ACK").Msg("msg3", false, 250, "FIN").Msg("msg4", true, 320, "ACK", Good);
                    s.Box("msgtw", "TIME_WAIT", Kit.SeqLeft - 80, 420, 160, 44, "2 x MSL", Warn);
                })
            .Step("Congestion control",
                "The congestion window (cwnd) limits how much unacknowledged data is in flight. It doubles every RTT in slow start, then grows linearly, and is cut in half on loss: additive increase, multiplicative decrease (AIMD).",
                "Linux defaults to CUBIC. Google's BBR estimates bottleneck bandwidth and RTT instead of reacting to loss. Congestion control is why one download does not crush everyone else on a shared link.",
                [4],
                s =>
                {
                    s.ClearMessages();
                    s.Remove("lc", "ls", "lcl", "lsl");
                    int[] cwnd = [1, 2, 4, 8, 16, 32, 33, 34, 35, 17, 18, 19];
                    s.LabeledBars("cw", cwnd.Select((c, i) => ($"RTT {i + 1}", (double)c, c.ToString())).ToArray(), 60, 480, 62, 12, 360);
                    for (var i = 0; i < cwnd.Length; i++)
                    {
                        s.Paint("cw" + i, i < 6 ? Info : i == 9 ? Bad : Good);
                    }

                    s.Say("Slow start (blue), additive increase (green), loss halves the window (red)", Info);
                })
            .Lesson("TCP: handshake, ACKs, loss and congestion", "Networking", "A sequence-diagram walk through connecting, sending, losing, retransmitting and closing.");

    // ------------------------------------------------------------------------------------------
    private static Lesson UdpQuic()
    {
        string[] streams = ["A", "B", "C", "A", "B", "C", "A", "B"];

        void Packets(SceneState s, string prefix, double y, bool quic)
        {
            for (var i = 0; i < streams.Length; i++)
            {
                var id = prefix + i;
                s.Box(id, streams[i], 240 + i * 90, y, 80, 50, i == 0 ? "lost" : null);
                s.Shape(id).Mono = true;
                s.Tint(id, streams[i][0] - 'A');
                var blocked = i > 0 && (!quic || streams[i] == "A");
                s.Paint(id, i == 0 ? Bad : blocked ? Warn : Good).Ghost(id, i == 0);
                s.SubText(id, i == 0 ? "lost" : blocked ? "waits" : "delivered");
            }
        }

        return new StoryBuilder("IO5", "UDP sends independent datagrams with no handshake or ordering. QUIC builds reliable, encrypted, multiplexed streams on top of UDP, removing TCP's head-of-line blocking and handshake delay. HTTP/3 runs on QUIC.")
            .Setup(s => s.Fields("udp", [("src port", "2 B", 1), ("dst port", "2 B", 1), ("length", "2 B", 1), ("checksum", "2 B", 1), ("payload", "DNS query, game state, voice...", 4)], 40, 30, 920, 60, Info))
            .Code("""
                using var udp = new UdpClient();
                await udp.SendAsync(query, "192.0.2.53", 53);     // one datagram, no handshake

                using var http = new HttpClient
                {
                    DefaultRequestVersion = HttpVersion.Version30,     // HTTP/3 over QUIC
                    DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
                };
                """)
            .Step("UDP: 8 bytes of header, nothing else",
                "Ports, a length and a checksum. No connection, no ordering, no retransmission. If a datagram is lost, it is simply gone.",
                "That is exactly right for DNS (one question, one answer), live voice and games (old data is useless), and as a base for new protocols. The application handles loss if it cares.",
                [1, 2])
            .Step("TCP head-of-line blocking",
                "HTTP/2 multiplexes streams A, B and C over one TCP connection. One lost packet from stream A makes TCP hold back everything after it, including B and C, until the retransmission arrives.",
                "TCP only knows about one ordered byte stream, not about the streams inside it. On lossy mobile networks this can make HTTP/2 slower than several HTTP/1.1 connections.",
                [],
                s =>
                {
                    s.Caption("tcap", "HTTP/2 over TCP", 40, 168, 190);
                    Packets(s, "t", 150, false);
                })
            .Step("QUIC: independent streams",
                "QUIC knows about streams. The same loss stalls only stream A; B and C are delivered immediately.",
                "QUIC (RFC 9000, 2021) runs in user space over UDP, so it can evolve without OS kernel or middlebox changes. Packet numbers are never reused, which makes RTT measurement and loss detection unambiguous.",
                [],
                s =>
                {
                    s.Caption("qcap", "HTTP/3 over QUIC", 40, 268, 190);
                    Packets(s, "q", 250, true);
                })
            .Step("Faster handshakes",
                "TCP + TLS 1.3 needs 2 round trips before the first request. QUIC combines transport and TLS 1.3 into 1 round trip, and 0 for resumed connections.",
                "On a 100 ms mobile RTT, that saves 100-200 ms per new connection. 0-RTT data can be replayed by an attacker, so it is only used for idempotent requests.",
                [5],
                s => s.LabeledBars("rtt", [("TCP + TLS 1.2", 3, "3 RTT"), ("TCP + TLS 1.3", 2, "2 RTT"), ("QUIC", 1, "1 RTT"), ("QUIC resumed", 0, "0 RTT")], 240, 520, 140, 40, 150))
            .Step("Connection migration and HTTP/3",
                "QUIC connections are identified by connection IDs, not by IP address and port, so a phone moving from Wi-Fi to 5G keeps its connections alive.",
                "HTTP/3 (RFC 9114, 2022) is supported by all major browsers and CDNs. .NET supports it via MsQuic. Multipath QUIC (using Wi-Fi and cellular at once) is being standardised in the IETF.",
                [4, 5, 6],
                s => s.Say("UDP underneath, reliable encrypted streams on top", Good))
            .Lesson("UDP, QUIC and HTTP/3", "Networking", "Datagrams, head-of-line blocking, QUIC streams, 1-RTT handshakes and connection migration.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson WiFi() =>
        new StoryBuilder("IO6", "Wi-Fi stations share one radio channel, so they listen before talking and back off randomly. Each generation adds wider channels, denser modulation and smarter scheduling, and Wi-Fi 8 targets reliability over peak speed.")
            .Setup(s =>
            {
                s.Circle("ap", "AP", 500, 90, 48, "access point", Info);
                string[] st = ["laptop", "phone", "TV", "camera"];
                for (var i = 0; i < 4; i++)
                {
                    s.Box("st" + i, st[i], 60 + i * 240, 220, 160, 56);
                    s.Connect("ap", "st" + i, dashed: true, arrow: false);
                }

                s.Caption("aircap", "the air over time (one channel)", 60, 300);
                s.Cells("air", Enumerable.Repeat("", 8).ToArray(), 60, 324, 106, 50, 4, tags: false);
            })
            .NoCode()
            .Step("Listen before talk (CSMA/CA)",
                "The laptop senses the channel is idle, waits a random backoff, then transmits. The AP acknowledges. Everyone else defers until the air is free.",
                "Radio is half-duplex and a station cannot detect collisions while transmitting, so Wi-Fi avoids them (collision avoidance) instead of detecting them like old Ethernet. Every unicast frame gets an ACK.",
                s =>
                {
                    s.Paint("st0", Info);
                    s.Move("shape:st0", "shape:ap", "data", Info);
                    s.Write("air0", "laptop").Paint("air0", Info).Write("air1", "ACK").Paint("air1", Good);
                })
            .Step("Collision and backoff",
                "The phone and the TV pick the same backoff slot and transmit together. Both frames are corrupted, no ACK comes back, and each doubles its random backoff range before retrying.",
                "Hidden nodes (two stations that cannot hear each other but both reach the AP) make this worse. RTS/CTS reservation helps. Busy networks lose a lot of airtime to contention.",
                s =>
                {
                    s.Paint("st0", Neutral).PaintAll(Warn, "st1", "st2");
                    s.Move("shape:st1", "shape:ap", "data", Warn).Move("shape:st2", "shape:ap", "data", Warn);
                    s.Write("air2", "phone + TV").Paint("air2", Bad).Write("air3", "backoff").Paint("air3", Neutral).Write("air4", "phone").Paint("air4", Info).Write("air5", "ACK").Paint("air5", Good);
                })
            .Step("OFDMA: share one transmission (Wi-Fi 6)",
                "Instead of one station per transmission, the AP splits the channel into resource units (groups of subcarriers) and serves several stations at once.",
                "OFDMA plus MU-MIMO (several spatial streams to different devices) fixed Wi-Fi's 'many small packets' problem in crowded homes and offices. Target Wake Time lets IoT devices sleep until a scheduled slot.",
                s =>
                {
                    s.PaintAll(Neutral, "st1", "st2");
                    for (var i = 0; i < 4; i++)
                    {
                        s.Box("ru" + i, $"RU {i + 1}: {new[] { "laptop", "phone", "TV", "camera" }[i]}", 60, 400 + i * 34, 324, 30);
                        s.Tint("ru" + i, i);
                        s.Move("shape:ap", "shape:st" + i, "RU " + (i + 1), Good);
                    }
                })
            .Step("Generations",
                "Peak PHY rates: 11 Mb/s (802.11b, 1999) to about 23 Gb/s (Wi-Fi 7). Real throughput is typically a third to a half of the peak.",
                "Gains come from wider channels (20 to 320 MHz), denser modulation (up to 4096-QAM = 12 bits per symbol), more spatial streams (MIMO) and new spectrum (6 GHz since Wi-Fi 6E).",
                s =>
                {
                    s.RemoveWhere(sh => sh.Id.StartsWith("ru", StringComparison.Ordinal) || sh.Id.StartsWith("air", StringComparison.Ordinal));
                    s.LabeledBars("g", [("b 1999", 11, "11M"), ("a/g 2003", 54, "54M"), ("4 (n) 2009", 600, "600M"), ("5 (ac) 2013", 6900, "6.9G"), ("6 (ax) 2019", 9600, "9.6G"), ("6E 2020", 9600, "9.6G 6GHz"), ("7 (be) 2024", 23000, "23G"), ("8 (bn) ~2028", 23000, "UHR")], 60, 530, 100, 14, 220, log: true);
                })
            .Step("Wi-Fi 7 and Wi-Fi 8",
                "Wi-Fi 7 (802.11be, IEEE-approved 2024) adds 320 MHz channels, 4096-QAM and Multi-Link Operation (using 5 and 6 GHz simultaneously). Wi-Fi 8 (802.11bn, 'Ultra High Reliability') is in draft, targeting about 2028.",
                "Wi-Fi 8 keeps roughly Wi-Fi 7's peak rate but targets about 25% better throughput, latency and packet loss in real conditions, with coordination between neighbouring APs.",
                s =>
                {
                    s.Paint("g6", Good).Paint("g7", Warn).Ghost("g7");
                    s.Say("Wi-Fi 8: reliability, not peak speed", Info);
                })
            .Lesson("Wi-Fi: sharing the air", "Wireless", "CSMA/CA, collisions, OFDMA, and Wi-Fi generations through Wi-Fi 7 and 8.");

    // ------------------------------------------------------------------------------------------
    private static Lesson Usb() =>
        new StoryBuilder("IO7", "USB is host-centred: the host enumerates every device, assigns it an address, reads its descriptors to load a driver, then schedules all traffic using four transfer types.")
            .Setup(s =>
            {
                s.Box("host", "host controller", 400, 30, 200, 60, "PC (xHCI)", Info);
                s.Box("hub", "hub", 400, 150, 200, 50);
                string[] devs = ["keyboard", "flash drive", "webcam", "headset"];
                for (var i = 0; i < 4; i++)
                {
                    s.Box("d" + i, devs[i], 40 + i * 240, 270, 200, 60);
                    s.Connect("hub", "d" + i, arrow: false);
                }

                s.Connect("host", "hub", arrow: false);
            })
            .NoCode()
            .Step("A tree with one master",
                "Devices connect through hubs in a tree (up to 127 devices per controller). The host starts every transaction; devices only answer when polled.",
                "Host scheduling keeps devices simple and cheap. USB 3.x and USB4 add more direct signalling, but the host still owns the bus.")
            .Step("Enumeration: who are you?",
                "A flash drive is plugged in. The host resets the port and talks to default address 0: GET_DESCRIPTOR returns vendor and product IDs and the device class. SET_ADDRESS gives it address 5.",
                "Then the host reads configuration descriptors (interfaces, endpoints, power needs) and loads a driver. Standard class drivers (HID, Mass Storage/UAS, CDC serial, Audio, Video) are why most devices work without vendor drivers.",
                s =>
                {
                    s.Paint("d1", Info);
                    s.Move("shape:host", "shape:d1", "GET_DESCRIPTOR @ addr 0", Info).Move("shape:d1", "shape:host", "VID/PID, class 0x08", Good);
                    s.Table("desc", "device descriptor", 40, 380, 440, "address    0 -> 5 (SET_ADDRESS)", "VID:PID    0x1234:0x5678", "class      0x08 mass storage", "driver     UAS / usbstor");
                })
            .Step("Four transfer types",
                "Control for setup. Interrupt for keyboards and mice (polled at a fixed interval). Bulk for drives (uses leftover bandwidth, retried on error). Isochronous for audio and video (reserved bandwidth, never retried).",
                "Isochronous traffic would rather drop a sample than arrive late. Bulk would rather be late than lose a byte. Choosing the right type is part of designing a USB device.",
                s =>
                {
                    s.Paint("d1", Good).Paint("d0", Info).PaintAll(Warn, "d2", "d3");
                    s.SubText("d0", "interrupt").SubText("d1", "bulk").SubText("d2", "isochronous").SubText("d3", "isochronous");
                    s.Table("xfer", "transfer types", 520, 380, 440, "control      setup, descriptors", "interrupt    keyboard, mouse, game pad", "bulk         storage, printers, network", "isochronous  audio, video, streams");
                })
            .Step("Speeds",
                "From 1.5 Mb/s (USB 1.0, 1996) to 80 Gb/s (USB4 version 2.0, 2022), which can also run asymmetrically at 120 Gb/s one way and 40 Gb/s the other.",
                "Encoding changed along the way: NRZI (USB 2.0), 8b/10b (5 Gb/s), 128b/132b (10-20 Gb/s), then PAM3 signalling for USB4 v2. Higher rates need USB-C and better cables.",
                s =>
                {
                    s.Remove("desc", "xfer");
                    s.LabeledBars("u", [("1.0 1996", 1.5, "1.5M"), ("1.1 1998", 12, "12M"), ("2.0 2000", 480, "480M"), ("3.0 2008", 5000, "5G"), ("3.1 2013", 10000, "10G"), ("3.2 2017", 20000, "20G"), ("USB4 2019", 40000, "40G"), ("USB4 v2 2022", 80000, "80G")], 40, 540, 100, 17, 190, log: true);
                })
            .Step("The naming mess",
                "USB 3.0 was renamed USB 3.1 Gen 1, then USB 3.2 Gen 1, all meaning 5 Gb/s. Marketing names now just state the speed: 'USB 5Gbps', '10Gbps', '20Gbps', '40Gbps', '80Gbps'.",
                "Look at the speed and power logos, not the version number. A USB-C port can be anything from USB 2.0 at 480 Mb/s to USB4 at 80 Gb/s (see the next lesson).",
                s => s.Say("Read the Gbps label, not the version number", Warn))
            .Lesson("USB: enumeration, transfer types and speeds", "Peripheral buses", "How a device is discovered and driven, the four transfer types, and USB 1.0 to USB4 v2.");

    // ------------------------------------------------------------------------------------------
    private static Lesson UsbC() =>
        new StoryBuilder("IO8", "USB-C is a connector, not a protocol. Over the same plug, Power Delivery negotiates up to 240 W, and USB4/Thunderbolt tunnel USB, DisplayPort and PCIe traffic through one high-speed link.")
            .Setup(s =>
            {
                s.Box("laptop", "laptop", 40, 190, 190, 120);
                s.Box("dock", "dock / charger", 770, 190, 190, 120);
                s.Region("cable", "USB-C cable (24-pin, reversible)", 250, 150, 500, 210);
                string[] lanes = ["VBUS: power", "CC: configuration + PD messages", "D+/D-: USB 2.0", "high-speed lane pair 1", "high-speed lane pair 2"];
                for (var i = 0; i < lanes.Length; i++)
                {
                    s.Box("ln" + i, lanes[i], 270, 180 + i * 34, 460, 28);
                }
            })
            .NoCode()
            .Step("One connector, many capabilities",
                "The USB-C plug carries power (VBUS), a configuration channel (CC), a USB 2.0 pair and two high-speed lane pairs. What a given port and cable actually support varies widely.",
                "A USB-C cable might be charge-only, USB 2.0, 10 Gb/s, 40 Gb/s or 80 Gb/s, rated for 3 A or 5 A. An 'e-marker' chip in the cable tells the devices what it can handle.")
            .Step("Power Delivery negotiation",
                "Over CC, the charger advertises what it can supply (power data objects). The laptop requests 20 V at 3.25 A (65 W), the charger accepts, then switches VBUS from 5 V to 20 V.",
                "Without negotiation, VBUS stays at a safe 5 V. Standard Power Range goes up to 20 V x 5 A = 100 W, and 5 A requires an e-marked cable. PPS (programmable supply) lets phones request voltage in 20 mV steps for efficient fast charging.",
                s =>
                {
                    s.Paint("ln1", Info).Paint("ln0", Good);
                    s.Move("shape:dock", "shape:laptop", "Source_Capabilities", Info).Move("shape:laptop", "shape:dock", "Request 20 V 3.25 A", Info).Move("shape:dock", "shape:laptop", "Accept, PS_RDY", Good);
                    s.Table("pdo", "charger PDOs", 40, 390, 440, "5 V   3 A   15 W", "9 V   3 A   27 W", "15 V  3 A   45 W", "20 V  5 A  100 W  (5 A: e-marked cable)").HotRow("pdo", 3);
                })
            .Step("Extended Power Range: 240 W",
                "USB PD 3.1 (2021) added 28, 36 and 48 V levels at up to 5 A, reaching 240 W for gaming laptops and monitors.",
                "Higher voltage means less current for the same power, so thinner conductors and less heat. EPR requires EPR-rated cables and extra safety checks. PD 3.2 (2023) refined adjustable-voltage modes, and newer revisions keep evolving.",
                s =>
                {
                    s.PaintAll(Neutral, "ln1", "ln0");
                    s.Table("pdo", "charger PDOs (EPR)", 40, 390, 440, "20 V  5 A  100 W  SPR max", "28 V  5 A  140 W", "36 V  5 A  180 W", "48 V  5 A  240 W  EPR max").HotRow("pdo", 3);
                })
            .Step("USB4: tunnels through one link",
                "USB4 does not just carry USB. It packetises USB 3.x, DisplayPort video and PCIe traffic and interleaves them over the same high-speed lanes, sharing bandwidth dynamically.",
                "That is how one cable to a dock can drive two monitors, an NVMe drive and a keyboard. DisplayPort Alt Mode is the older alternative: it hands some lanes to raw DisplayPort signals.",
                s =>
                {
                    s.Remove("pdo");
                    s.PaintAll(Info, "ln3", "ln4");
                    s.Write("ln3", "USB4: [DP] [PCIe] [USB3] [DP] [PCIe] ...").Write("ln4", "USB4: [DP] [USB3] [DP] [PCIe] [DP] ...");
                    s.Move("shape:laptop", "shape:dock", "DisplayPort tunnel", Warn).Move("shape:laptop", "shape:dock", "PCIe tunnel (NVMe)", Info).Move("shape:dock", "shape:laptop", "USB 3 tunnel", Good);
                })
            .Step("Thunderbolt",
                "Thunderbolt 3 (2015) and 4 (2020) run at 40 Gb/s. Thunderbolt 4 is built on USB4 with stricter minimums. Thunderbolt 5 (2023) runs at 80 Gb/s, or 120 Gb/s one way with Bandwidth Boost, matching USB4 version 2.0.",
                "Thunderbolt certification guarantees features that USB4 leaves optional, such as PCIe tunnelling, dual 4K displays and minimum power. Thunderbolt 5 tunnels PCIe 4.0 x4, which is enough for external GPUs and fast SSDs.",
                s =>
                {
                    s.Table("tb", "USB4 / Thunderbolt", 40, 390, 920,
                        "Thunderbolt 3  2015   40 Gb/s",
                        "USB4 v1        2019   20 / 40 Gb/s, tunnelling",
                        "Thunderbolt 4  2020   40 Gb/s, USB4-based, guaranteed PCIe + 2 displays",
                        "USB4 v2        2022   80 Gb/s symmetric or 120/40 asymmetric (PAM3)",
                        "Thunderbolt 5  2023   80 Gb/s, 120 Gb/s boost, PCIe 4.0 x4").HotRow("tb", 4);
                })
            .Lesson("USB-C, Power Delivery, USB4 and Thunderbolt", "Peripheral buses", "Connector vs protocol, PD negotiation up to 240 W, USB4 tunnelling and Thunderbolt 5.");

    // ------------------------------------------------------------------------------------------
    private static Lesson Bluetooth() =>
        new StoryBuilder("IO9", "Bluetooth has two radios: Classic for continuous audio, and Low Energy for everything that sleeps most of the time. LE devices advertise on 3 channels, hop across the 2.4 GHz band, and expose data as GATT services.")
            .Setup(s =>
            {
                s.Box("phone", "phone", 40, 30, 220, 70, "central");
                s.Box("sensor", "heart-rate strap", 740, 30, 220, 70, "peripheral");
                s.Caption("chcap", "2.4 GHz band: 40 LE channels of 2 MHz (2402-2480 MHz); 37, 38, 39 = advertising", 40, 140, 700);
                s.Cells("ch", Enumerable.Repeat("", 40).ToArray(), 40, 175, 20, 40, 3, tags: false);
                foreach (var (pos, name) in new[] { (0, "37"), (12, "38"), (39, "39") })
                {
                    s.Label("adv" + name, name, 40 + pos * 23, 220, 30, 11);
                }
            })
            .NoCode()
            .Step("Classic and Low Energy",
                "Classic (BR/EDR) uses 79 channels of 1 MHz for streaming audio and calls, at up to 3 Mb/s. Low Energy (since 4.0, 2010) uses 40 channels of 2 MHz and can run for years on a coin cell.",
                "Most phones and headphones support both ('dual mode'). LE is not just 'low power Classic': it is a different protocol stack designed for short bursts and long sleeps.")
            .Step("Advertising",
                "The strap broadcasts small advertising packets on channels 37, 38 and 39, which are spread across the band to dodge Wi-Fi. The phone scans, sees 'Heart Rate', and sends a connection request.",
                "Advertising also powers beacons, contact tracing and Find My networks without any connection. Bluetooth 5 added extended advertising with bigger payloads on secondary channels.",
                s =>
                {
                    s.PaintAll(Warn, "ch0", "ch12", "ch39");
                    s.Move("shape:sensor", "shape:phone", "ADV_IND: Heart Rate", Warn).Move("shape:phone", "shape:sensor", "CONNECT_IND", Info);
                })
            .Step("Connection events and hopping",
                "Once connected, the two wake up at agreed intervals (connection events), exchange packets, and sleep. Each event hops to a different data channel.",
                "Adaptive frequency hopping marks channels that are busy with Wi-Fi as unused. Longer intervals save power, and Bluetooth 6.2 added shorter intervals for lower latency.",
                s =>
                {
                    s.PaintAll(Neutral, "ch0", "ch12", "ch39");
                    s.PaintAll(Good, "ch5", "ch17", "ch29", "ch8", "ch33", "ch22");
                    s.Move("shape:phone", "shape:sensor", "event @ ch 4", Info).Move("shape:sensor", "shape:phone", "data", Good);
                })
            .Step("GATT: services and characteristics",
                "Data is organised as services containing characteristics, each identified by a UUID. The Heart Rate service is 0x180D, and its measurement characteristic 0x2A37 notifies the phone of every beat.",
                "Standard profiles mean any app can read any compliant heart-rate strap, thermometer or battery level (0x180F / 0x2A19). Custom devices use 128-bit UUIDs.",
                s =>
                {
                    s.PaintAll(Neutral, "ch5", "ch17", "ch29", "ch8", "ch33", "ch22");
                    s.Table("gatt", "GATT database on the strap", 40, 250, 520, "Service 0x180D  Heart Rate", "  Char 0x2A37  Measurement   [notify]", "  Char 0x2A38  Sensor location [read]", "Service 0x180F  Battery", "  Char 0x2A19  Battery level [read, notify]").HotRow("gatt", 1);
                    s.Move("shape:sensor", "shape:phone", "notify 0x2A37: 72 bpm", Good);
                })
            .Step("LE Audio and Auracast",
                "Bluetooth 5.2 added isochronous channels for LE Audio with the LC3 codec: better quality at lower bitrates, true multi-stream earbuds, hearing aids, and Auracast broadcast to any number of listeners.",
                "Auracast turns a TV, airport gate or lecture hall into a broadcaster that any compatible earbuds can join, like tuning a radio.",
                s =>
                {
                    s.Remove("gatt");
                    s.Box("bc", "Auracast broadcaster", 360, 270, 280, 56, "LC3 audio stream", Info);
                    for (var i = 0; i < 4; i++)
                    {
                        s.Box("rx" + i, "earbuds", 60 + i * 240, 400, 160, 50);
                        s.Move("shape:bc", "shape:rx" + i, i == 0 ? "broadcast" : "", Good);
                    }
                })
            .Step("Versions up to today",
                "Each version adds features while staying backward compatible. Bluetooth 6.0 (2024) added Channel Sounding for secure centimetre-level distance measurement, for example for digital car keys.",
                "Versions 6.1 to 6.3 (2025-2026) improved privacy, power and latency. The next major work item, High Data Throughput, aims to multiply LE data rates. It is in development, so treat early details as provisional.",
                s =>
                {
                    s.RemoveWhere(sh => sh.Id == "bc" || sh.Id.StartsWith("rx", StringComparison.Ordinal));
                    s.Table("ver", "Bluetooth Core versions", 40, 250, 920,
                        "4.0  2010  Bluetooth Low Energy",
                        "4.2  2014  LE Secure Connections, larger packets",
                        "5.0  2016  2M PHY, long-range Coded PHY, extended advertising",
                        "5.1  2019  direction finding (angle of arrival/departure)",
                        "5.2  2019  isochronous channels -> LE Audio, LC3, Auracast",
                        "5.3 / 5.4  2021 / 2023  subrating, PAwR (electronic shelf labels)",
                        "6.0  2024  Channel Sounding (secure distance)",
                        "6.1-6.3  2025-2026  privacy, power, shorter connection intervals",
                        "next  in development  High Data Throughput (HDT)").HotRow("ver", 6, 7);
                })
            .Lesson("Bluetooth: Classic, LE, GATT and LE Audio", "Wireless", "Advertising, channel hopping, GATT services, Auracast and versions through 6.x.");

    // ------------------------------------------------------------------------------------------
    private static Lesson Pcie() =>
        new StoryBuilder("IO10", "PCI Express connects CPUs to GPUs, SSDs and NICs with point-to-point serial lanes carrying packets. Bandwidth doubles every generation; NVMe and CXL build on it for storage and memory.")
            .Setup(s =>
            {
                s.Box("cpu", "CPU root complex", 350, 30, 300, 70, tone: Info);
                s.Box("gpu", "GPU", 40, 220, 260, 70);
                s.Box("nvme", "NVMe SSD", 370, 220, 260, 70);
                s.Box("nic", "400G NIC", 700, 220, 260, 70);
                s.Connect("cpu", "gpu", "x16", arrow: false).Connect("cpu", "nvme", "x4", arrow: false).Connect("cpu", "nic", "x16", arrow: false);
            })
            .NoCode()
            .Step("Lanes and links",
                "A lane is two differential pairs, one per direction, so links are full duplex. Links bundle x1, x4, x8 or x16 lanes; a GPU typically gets x16 and an SSD x4.",
                "Unlike old shared PCI buses, every link is point-to-point. Switches fan out to more devices. Slots are backward compatible: a Gen 5 card works in a Gen 3 slot at Gen 3 speed.")
            .Step("Packets, not wires",
                "Reads, writes and DMA travel as transaction layer packets (TLPs) with a header, payload and CRCs. The data link layer ACKs every packet and replays any that are corrupted.",
                "An SSD writes to RAM directly by DMA: it sends memory-write TLPs to the root complex, and the CPU never copies the data. Doorbells and MSI-X interrupts are just small writes too.",
                s =>
                {
                    s.Fields("tlp", [("framing", "STP", 1), ("sequence", "2 B", 1), ("TLP header", "12-16 B", 2), ("payload", "0-4096 B", 4), ("ECRC (opt.)", "4 B", 1), ("LCRC", "4 B", 1)], 40, 400, 920, 60, Info);
                    s.Move("shape:nvme", "shape:cpu", "DMA write 4 KiB", Good);
                })
            .Step("Generations: doubling every time",
                "Per-lane rate: 2.5 GT/s (1.0, 2003) to 128 GT/s (7.0, 2025). PCIe 8.0 targets 256 GT/s around 2028, about 1 TB/s in both directions combined on x16.",
                "Gen 5 x16 moves about 63 GB/s each way, roughly the bandwidth of two channels of DDR5. Gens 6 and 7 are first used in AI servers, and consumer PCs adopt them years later.",
                s =>
                {
                    s.Remove("tlp0", "tlp1", "tlp2", "tlp3", "tlp4", "tlp5");
                    s.LabeledBars("gen", [("1.0 2003", 2.5, "2.5"), ("2.0 2007", 5, "5"), ("3.0 2010", 8, "8"), ("4.0 2017", 16, "16"), ("5.0 2019", 32, "32"), ("6.0 2022", 64, "64"), ("7.0 2025", 128, "128"), ("8.0 ~2028", 256, "256")], 40, 530, 100, 17, 190, log: true);
                    s.Paint("gen7", Warn).Ghost("gen7");
                })
            .Step("Encoding: from 8b/10b to PAM4 and FLITs",
                "Gens 1-2 sent 10 bits for every 8 (20% overhead). Gens 3-5 use 128b/130b. Gen 6 switched to PAM4 (2 bits per symbol) with fixed-size FLITs and forward error correction.",
                "Raising the rate per lane is getting much harder, so PAM4 doubles bits per symbol instead of doubling frequency. FEC fixes the bit errors that denser signalling introduces, at a small latency cost.",
                s =>
                {
                    s.PaintAll(Info, "gen0", "gen1");
                    s.PaintAll(Good, "gen2", "gen3", "gen4");
                    s.PaintAll(Warn, "gen5", "gen6");
                })
            .Step("NVMe: queues per core",
                "NVMe gives each CPU core its own submission and completion queues in RAM: up to 64K queues of 64K commands, versus AHCI/SATA's single queue of 32. No locking between cores.",
                "A core writes a command into its queue and rings a doorbell register; the SSD fetches it by DMA and posts a completion. This parallelism is how modern SSDs reach millions of IOPS.",
                s =>
                {
                    s.RemoveWhere(sh => sh.Id.StartsWith("gen", StringComparison.Ordinal));
                    for (var i = 0; i < 4; i++)
                    {
                        s.Box("core" + i, $"core {i}", 40 + i * 240, 400, 200, 60, $"SQ {i} + CQ {i}", Info);
                        s.Connect("core" + i, "nvme", arrow: false, dashed: true);
                    }

                    s.Paint("nvme", Good);
                })
            .Step("CXL, NVLink and chiplet links",
                "CXL runs over the PCIe physical layer but adds cache-coherent memory access: servers can add or pool memory over CXL. CXL 4.0 (2025) moves to 128 GT/s.",
                "GPUs talk to each other over NVLink (5th generation: 1.8 TB/s per GPU; the 6th generation targets 3.6 TB/s). Inside packages, UCIe standardises die-to-die links between chiplets from different vendors.",
                s =>
                {
                    s.RemoveWhere(sh => sh.Id.StartsWith("core", StringComparison.Ordinal));
                    s.Paint("nvme", Neutral);
                    s.Table("links", "beyond PCIe", 40, 360, 920,
                        "CXL 4.0      2025   coherent memory expansion / pooling over PCIe PHY",
                        "NVLink 5     2024   GPU-GPU, 1.8 TB/s per GPU   (NVLink 6: 3.6 TB/s)",
                        "UCIe 3.0     2025   die-to-die chiplet links (48/64 GT/s)",
                        "NVMe 2.x     ongoing  storage command set over PCIe and fabrics");
                })
            .Lesson("PCI Express, NVMe and CXL", "Peripheral buses", "Lanes, packets, generations 1.0 to 8.0, NVMe queues, and CXL/NVLink/UCIe.");

    // ------------------------------------------------------------------------------------------
    private static Lesson Display() =>
        new StoryBuilder("IO11", "Displays need huge, steady bit rates: every pixel, every frame. HDMI and DisplayPort keep raising link capacity, and Display Stream Compression covers the rest.")
            .Setup(s => s.LabeledBars("need", [("1080p60 8-bit", 3.0, "3.0"), ("1440p144 10-bit", 15.9, "15.9"), ("4K60 10-bit", 14.9, "14.9"), ("4K144 10-bit", 35.8, "35.8"), ("8K60 10-bit", 59.7, "59.7")], 80, 300, 150, 40, 220))
            .NoCode()
            .Step("How many bits does a screen need?",
                "width x height x refresh x bits per pixel. 4K at 60 Hz with 10-bit colour: 3840 x 2160 x 60 x 30 = 14.9 Gb/s of pixels, before blanking intervals and encoding overhead.",
                "Bars show active pixel data in Gb/s. A 4K 144 Hz HDR monitor needs about 36 Gb/s, and 8K60 about 60 Gb/s, which explains why cable standards keep leaping.")
            .Step("HDMI: TMDS to FRL",
                "HDMI 2.0 used TMDS (3 data channels plus a clock) at up to 18 Gb/s. HDMI 2.1 switched to packetised FRL over 4 lanes at 48 Gb/s, and HDMI 2.2 (2025) doubles that to 96 Gb/s.",
                "HDMI dominates TVs and consoles. It also carries audio, CEC remote control and eARC for soundbars. Higher rates need certified 'Ultra High Speed' cables.",
                s => s.Table("hdmi", "HDMI", 60, 380, 420, "1.4  2009  10.2 Gb/s  TMDS", "2.0  2013  18 Gb/s    TMDS", "2.1  2017  48 Gb/s    FRL, 4 lanes", "2.2  2025  96 Gb/s    FRL").HotRow("hdmi", 3))
            .Step("DisplayPort: packets and daisy chains",
                "DisplayPort was packet-based from the start, with 4 main lanes plus an AUX channel. MST daisy-chains several monitors from one port. DP 2.1 reaches 80 Gb/s (UHBR20).",
                "DisplayPort is native to GPUs and PCs, and it is what USB-C/Thunderbolt carries for video. DP 2.1a/2.1b (2024-2025) added certified longer cables for UHBR speeds.",
                s => s.Table("dp", "DisplayPort", 520, 380, 440, "1.2  2010  21.6 Gb/s  MST", "1.4  2016  32.4 Gb/s  DSC", "2.1  2022  80 Gb/s    UHBR20", "2.1b 2025  80 Gb/s    longer cables").HotRow("dp", 2, 3))
            .Step("DSC: visually lossless compression",
                "Display Stream Compression shrinks the stream about 3:1 with no visible loss and very low latency, so 4K144 (35.8 Gb/s) fits in about 12 Gb/s.",
                "DSC is how 4K240 and 8K run over today's links. Without it, cable bandwidth would be the bottleneck for every high-end monitor.",
                s =>
                {
                    s.Write("need3", "35.8 -> ~12 DSC").Paint("need3", Good);
                })
            .Step("Link capacity vs demand",
                "Maximum link rates in Gb/s. Compare them with the demands above.",
                "HDCP encrypts protected content on the link, which is why some captures show black screens. Both standards keep backward compatibility through link training: the two ends negotiate the best rate the cable sustains.",
                s =>
                {
                    s.Remove("hdmi", "dp");
                    s.LabeledBars("cap", [("HDMI 2.0", 18, "18"), ("DP 1.4", 32.4, "32.4"), ("HDMI 2.1", 48, "48"), ("DP 2.1", 80, "80"), ("HDMI 2.2", 96, "96")], 80, 540, 150, 40, 170);
                    s.PaintAll(Info, "cap0", "cap2", "cap4");
                    s.PaintAll(Good, "cap1", "cap3");
                })
            .Lesson("Display links: HDMI and DisplayPort", "Peripheral buses", "Pixel bandwidth maths, TMDS vs FRL, DisplayPort MST, DSC, and HDMI 2.2 / DP 2.1.");

    // ------------------------------------------------------------------------------------------
    private static Lesson EmbeddedBuses()
    {
        void Wave(SceneState s)
        {
            (string Name, int Bit)[] bits =
            [
                ("idle", 1), ("start", 0), ("b0", 1), ("b1", 0), ("b2", 0), ("b3", 0), ("b4", 0), ("b5", 0), ("b6", 1), ("b7", 0), ("stop", 1), ("idle", 1)
            ];
            for (var i = 0; i < bits.Length; i++)
            {
                var id = "wv" + i;
                s.Box(id, bits[i].Bit.ToString(), 40 + i * 78, bits[i].Bit == 1 ? 120 : 200, 74, 50, tone: bits[i].Name is "start" or "stop" ? Warn : bits[i].Bit == 1 ? Info : Neutral);
                s.TagText(id, bits[i].Name);
            }
        }

        const string IdA = "00011110011";
        const string IdB = "00011110111";

        return new StoryBuilder("IO12", "Inside devices and vehicles, simple buses connect chips: UART for serial consoles, I2C and SPI for sensors and flash, and CAN for cars. Each trades wires, speed and addressing differently.")
            .Setup(Wave)
            .NoCode()
            .Step("UART: start, 8 data bits, stop",
                "The line idles high. A start bit (0) marks the beginning, then 8 data bits least significant first, then a stop bit (1). Here 'A' = 0x41 = 0100 0001 is sent as 1,0,0,0,0,0,1,0.",
                "There is no clock wire: both sides must agree on the baud rate. At 115200 baud each bit lasts 8.68 µs, and 10 bits per byte give 11,520 bytes/s. RS-232 and RS-485 are electrical standards for the same framing.",
                s => s.Say("UART frame for 'A' (0x41), LSB first", Info))
            .Step("I2C: two wires, addressed devices",
                "SDA (data) and SCL (clock) are shared by many chips. The controller sends a start, a 7-bit address (0x48), read/write, and each byte is acknowledged by the receiver.",
                "Open-drain wiring lets any device pull the line low safely. Speeds: 100 kHz, 400 kHz, 1 MHz and 3.4 MHz. I3C (v1.2, 2025) is the faster successor at up to 12.5 MHz, with in-band interrupts.",
                s =>
                {
                    s.Wipe();
                    string[] devs = ["MCU (controller)", "temp sensor 0x48", "EEPROM 0x50", "RTC 0x68"];
                    for (var i = 0; i < 4; i++)
                    {
                        s.Box("dev" + i, devs[i], 40 + i * 240, 60, 200, 56, tone: i == 1 ? Info : Neutral);
                    }

                    s.Box("sda", "SDA", 40, 160, 920, 22);
                    s.Box("scl", "SCL", 40, 196, 920, 22);
                    s.Fields("i2c", [("start", "S", 0.8), ("address", "1001000 (0x48)", 3), ("R/W", "0", 0.8), ("ack", "A", 0.8), ("register", "0x00", 1.6), ("ack", "A", 0.8), ("data", "0x19 = 25 °C", 2.4), ("nack", "N", 0.8), ("stop", "P", 0.8)], 40, 280, 920, 60, Info);
                    s.Paint("i2c3", Good).Paint("i2c5", Good);
                })
            .Step("SPI: fast, full duplex, chip selects",
                "SPI uses a clock (SCLK), data out (MOSI), data in (MISO) and one chip-select line per device. Data shifts both ways at once on every clock edge.",
                "No addresses and no acknowledgements, so it is simple and fast: tens of MHz, and quad-SPI flash at 100+ MHz with 4 data lines. The cost is an extra wire per device.",
                s =>
                {
                    s.Wipe();
                    s.Box("ctl", "controller", 40, 140, 200, 200, tone: Info);
                    s.Box("flash", "flash chip", 740, 80, 220, 100);
                    s.Box("adc", "ADC", 740, 300, 220, 100);
                    s.Connect("ctl", "flash", "SCLK, MOSI, MISO (shared)").Connect("ctl", "adc", "SCLK, MOSI, MISO (shared)");
                    s.Box("cs0", "CS0 low: flash selected", 330, 420, 300, 36, tone: Good);
                    s.Box("cs1", "CS1 high: ADC idle", 330, 470, 300, 36);
                    s.Connect("ctl", "cs0", arrow: false, dashed: true).Connect("ctl", "cs1", arrow: false, dashed: true);
                    s.Move("shape:ctl", "shape:flash", "0x03 READ", Info).Move("shape:flash", "shape:ctl", "data", Good);
                })
            .Step("CAN: lowest ID wins arbitration",
                "Two ECUs start sending at once. A dominant 0 overrides a recessive 1 on the bus. At bit 8, ID 0x0F7 sends 1 but sees 0, so it backs off silently. ID 0x0F3 continues with no data lost.",
                "This non-destructive arbitration makes CAN deterministic: lower IDs are higher priority (brakes before radio). Classic CAN carries 8 bytes at up to 1 Mb/s over a twisted pair.",
                s =>
                {
                    s.Wipe();
                    s.Caption("caa", "ECU 1: ID 0x0F3", 40, 70, 200);
                    s.Caption("cab", "ECU 2: ID 0x0F7", 40, 150, 200);
                    s.Caption("cbus", "bus (0 dominant)", 40, 230, 200);
                    for (var i = 0; i < 11; i++)
                    {
                        var bus = (IdA[i] == '0' || IdB[i] == '0') ? '0' : '1';
                        s.Box("ca" + i, IdA[i].ToString(), 230 + i * 64, 60, 58, 46, tone: i == 8 ? Good : Neutral);
                        s.Box("cb" + i, IdB[i].ToString(), 230 + i * 64, 140, 58, 46, tone: i == 8 ? Bad : Neutral);
                        s.Box("cc" + i, bus.ToString(), 230 + i * 64, 220, 58, 46, tone: Info);
                        s.Ghost("cb" + i, i > 8);
                    }

                    s.Say("ECU 2 loses at bit 8 and retries after the frame", Warn);
                })
            .Step("Faster successors",
                "CAN FD carries up to 64 bytes with a faster data phase; CAN XL (ISO 11898-1:2024) carries up to 2048 bytes at 10-20+ Mb/s. Cars are also moving to automotive Ethernet (10BASE-T1S to multi-gigabit).",
                "Modern vehicles have about 100 ECUs. Zonal architectures replace much of the point-to-point wiring with Ethernet backbones, while CAN remains for robust, low-cost control.",
                s =>
                {
                    s.Wipe();
                    s.Table("emb", "embedded and automotive buses", 40, 60, 920,
                        "UART        2 wires (TX/RX)      up to a few Mb/s   debug consoles, GPS, modems",
                        "I2C / I3C   2 wires, addressed   0.1-3.4 MHz / 12.5 MHz   sensors, EEPROMs, PMICs",
                        "SPI / QSPI  4+ wires, CS each    10-100+ MHz        flash, displays, ADCs",
                        "CAN         2-wire diff. bus     1 Mb/s, 8 B        powertrain, body control",
                        "CAN FD      same bus             ~5-8 Mb/s, 64 B    newer ECUs",
                        "CAN XL      ISO 11898-1:2024     10-20+ Mb/s, 2048 B  zonal gateways",
                        "10BASE-T1S  1 pair, multidrop    10 Mb/s Ethernet   sensors/actuators on Ethernet").HotRow("emb", 5, 6);
                })
            .Lesson("Embedded buses: UART, I2C, SPI and CAN", "Embedded and IoT", "A UART waveform, I2C addressing, SPI chip selects, CAN arbitration and CAN XL.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson IotAndCellular()
    {
        static double X(double metres) => 120 + (Math.Log10(metres) + 2) / 7 * 820;
        static double Y(double bps) => 500 - (Math.Log10(bps) - 2) / 9 * 440;

        (string Id, string Name, double Range, double Rate, int Color)[] techs =
        [
            ("nfc", "NFC", 0.04, 424e3, 0),
            ("uwb", "UWB", 20, 6.8e6, 1),
            ("ble", "Bluetooth LE", 50, 2e6, 2),
            ("zig", "Zigbee / Thread", 30, 250e3, 3),
            ("wifi", "Wi-Fi 7", 50, 23e9, 4),
            ("lora", "LoRaWAN", 10_000, 5e3, 5),
            ("nb", "NB-IoT / LTE-M", 10_000, 200e3, 6),
            ("g5", "5G NR", 1_000, 10e9, 7)
        ];

        void Focus(SceneState s, params string[] ids)
        {
            foreach (var t in techs)
            {
                s.Ghost(t.Id, ids.Length > 0 && !ids.Contains(t.Id));
            }
        }

        return new StoryBuilder("IO13", "Wireless links trade range, data rate and power. NFC touches, Bluetooth and Thread cover rooms, Wi-Fi covers homes, LoRaWAN and cellular IoT cover kilometres, and 5G (with 6G next) covers everything at scale.")
            .Setup(s =>
            {
                s.Box("xaxis", null, 120, 500, 820, 2);
                s.Box("yaxis", null, 120, 60, 2, 440);
                string[] xt = ["1 cm", "10 cm", "1 m", "10 m", "100 m", "1 km", "10 km", "100 km"];
                for (var i = 0; i < xt.Length; i++)
                {
                    s.Label("xt" + i, xt[i], X(Math.Pow(10, i - 2)) - 20, 508, 70, 10.5);
                }

                string[] yt = ["100 b/s", "10 kb/s", "1 Mb/s", "100 Mb/s", "10 Gb/s"];
                for (var i = 0; i < yt.Length; i++)
                {
                    s.Label("yt" + i, yt[i], 30, Y(Math.Pow(10, 2 + i * 2)) - 8, 85, 10.5);
                }

                s.Caption("ax", "range (log) vs peak data rate (log)", 600, 30, 340);
                foreach (var t in techs)
                {
                    s.Circle(t.Id, null, X(t.Range), Y(t.Rate), 14, t.Name);
                    s.Tint(t.Id, t.Color);
                }
            })
            .NoCode()
            .Step("Range vs rate",
                "Each technology sits somewhere on this map. Up and to the right costs power, spectrum or infrastructure.",
                "Physics drives the trade-off: higher frequencies and wider channels carry more data but travel less far and are blocked by walls. Low-power protocols send tiny packets rarely and sleep the rest of the time.")
            .Step("802.15.4 meshes: Zigbee and Thread",
                "Zigbee and Thread both run on IEEE 802.15.4 radios (250 kb/s at 2.4 GHz). Mains-powered devices relay for each other, forming self-healing meshes across a house.",
                "Thread carries real IPv6 (via 6LoWPAN compression) and reaches the internet through a border router, often built into smart speakers and hubs. Thread 1.4 (2024) improved border-router interoperability.",
                s => Focus(s, "zig"))
            .Step("Matter: one smart-home language",
                "Matter is an application layer that runs over Wi-Fi, Thread or Ethernet, with Bluetooth LE for setup. A Matter bulb works with Apple, Google, Amazon and Samsung ecosystems at once.",
                "Matter 1.0 shipped in 2022, and frequent updates since (1.5 in late 2025, 1.6 in 2026) add device types. It standardises what the device says, not the radio it uses.",
                s =>
                {
                    Focus(s, "wifi", "ble", "zig");
                    s.Box("matter", "Matter (application layer)", 300, 400, 300, 50, "over Wi-Fi, Thread, Ethernet", Good);
                })
            .Step("Touch, find, and reach far",
                "NFC (13.56 MHz, a few cm, up to 424 kb/s) handles payments and pairing. UWB measures distance to centimetres for car keys and trackers. LoRaWAN sends a few bytes over 2-15 km on a battery that lasts years.",
                "NB-IoT and LTE-M reuse cellular networks for meters and trackers. Each of these optimises one extreme: proximity security, precise ranging, or range per milliwatt.",
                s =>
                {
                    s.Remove("matter");
                    Focus(s, "nfc", "uwb", "lora", "nb");
                })
            .Step("Cellular: 5G, 5G-Advanced, 6G",
                "5G NR (3GPP Release 15, 2018) added wide channels, millimetre wave and network slicing. 5G-Advanced (Release 18, 2024, and onwards) adds AI-driven networks and better positioning.",
                "6G is being specified now: the first 6G specifications in 3GPP Release 21 are targeted for about end-2028, with commercial networks expected around 2030. Integrated sensing and AI-native air interfaces are key themes.",
                s =>
                {
                    Focus(s, "g5");
                    s.Say("5G today, 5G-Advanced rolling out, 6G standards in development", Info);
                })
            .Lesson("Wireless IoT and cellular: Thread, Matter, NFC, LoRa, 5G/6G", "Embedded and IoT", "A range-vs-rate map of every wireless link, from NFC to 6G.");
    }

    // ------------------------------------------------------------------------------------------
    private static Lesson Atlas()
    {
        void Show(SceneState s, string title, params string[] rows)
        {
            s.Table("atlas", title, 30, 60, 940, [Row("interface", "latest released", "in development"), .. rows]).HotRow("atlas", 0);
        }

        return new StoryBuilder("IO14", "Every interface follows the same pattern: a new generation roughly every few years, each doubling bandwidth by adding lanes, symbols per second or bits per symbol, while staying backward compatible.")
            .Setup(s => Show(s, "wired networking",
                Row("Ethernet", "800GbE IEEE 802.3df (2024)", "1.6TbE 802.3dj (final ballots)"),
                Row("Ultra Ethernet", "UEC 1.0 (2025)", "further revisions"),
                Row("TCP/IP", "QUIC RFC 9000, HTTP/3 RFC 9114", "Multipath QUIC (IETF)")))
            .NoCode()
            .Step("Wired networking",
                "Ethernet is moving from 800 Gb/s to 1.6 Tb/s, driven by AI data centres. On top, QUIC and HTTP/3 are replacing TCP for much web traffic.",
                "'Latest released' means a published standard. 'In development' means drafts or announced targets, whose dates can slip.")
            .Step("Wireless",
                "Wi-Fi 7 is shipping and Wi-Fi 8 is in draft. Bluetooth is on the 6.x series. Cellular is in the 5G-Advanced era, with 6G specifications in progress.",
                "Wireless generations take longer because spectrum regulation and certification programmes must follow the IEEE or 3GPP standard.",
                s => Show(s, "wireless",
                    Row("Wi-Fi", "Wi-Fi 7 802.11be (2024)", "Wi-Fi 8 802.11bn (~2028)"),
                    Row("Bluetooth", "Core 6.x (6.0 2024 ... 6.3 2026)", "High Data Throughput"),
                    Row("Thread/Matter", "Thread 1.4 / Matter 1.6", "continuous releases"),
                    Row("Cellular", "5G-Advanced (Rel-18/19)", "6G (Rel-21, ~2028-2030)")))
            .Step("Peripheral and storage",
                "USB4 v2 and Thunderbolt 5 reach 80-120 Gb/s over USB-C. PCIe 7.0 is published and 8.0 is in development. NVMe keeps evolving, while SATA has stayed at 6 Gb/s.",
                "SATA's ceiling is why SSDs moved to NVMe over PCIe. USB PD revisions keep refining power negotiation up to 240 W.",
                s => Show(s, "peripheral and storage",
                    Row("USB", "USB4 v2.0, 80 Gb/s (2022)", "-"),
                    Row("Thunderbolt", "Thunderbolt 5, 80/120 Gb/s (2023)", "-"),
                    Row("USB PD", "PD 3.x, up to 240 W EPR", "ongoing revisions"),
                    Row("PCIe", "7.0, 128 GT/s (2025)", "8.0, 256 GT/s (~2028)"),
                    Row("NVMe", "NVMe 2.x", "ongoing"),
                    Row("SATA", "3.x, 6 Gb/s (since 2009)", "no faster SATA planned")))
            .Step("Display and chip-to-chip",
                "HDMI 2.2 (96 Gb/s) and DisplayPort 2.1 (80 Gb/s) lead displays. CXL, NVLink and UCIe connect CPUs, GPUs, memory and chiplets.",
                "Chip-to-chip links now evolve fastest of all, because AI systems are limited by how quickly accelerators can share data.",
                s => Show(s, "display and chip-to-chip",
                    Row("HDMI", "2.2, 96 Gb/s (2025)", "-"),
                    Row("DisplayPort", "2.1b, 80 Gb/s (2025)", "-"),
                    Row("CXL", "4.0, 128 GT/s (2025)", "-"),
                    Row("NVLink", "5th gen, 1.8 TB/s per GPU", "6th gen, 3.6 TB/s"),
                    Row("UCIe", "3.0 (2025) and later", "ongoing")))
            .Step("Embedded and automotive",
                "Even the slow buses evolve: I3C replaces I2C, CAN XL extends CAN, and single-pair Ethernet reaches sensors in cars and factories.",
                "Embedded standards prize longevity, so new versions arrive slowly and coexist with 30-year-old designs that are still shipping.",
                s => Show(s, "embedded and automotive",
                    Row("CAN", "CAN XL (ISO 11898-1:2024)", "-"),
                    Row("I3C", "v1.2 (2025)", "-"),
                    Row("SPE", "10BASE-T1S multidrop (2019)", "multi-gig automotive Ethernet"),
                    Row("UART", "unchanged since the 1960s", "-")))
            .Lesson("The interface atlas: latest and next", "Overview", "Every interface in one table: the newest released version and what is in development.");
    }
}
