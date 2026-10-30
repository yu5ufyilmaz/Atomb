param([string]$Type = 'ping', [string]$Parameters = '{}', [int]$Port = 6400)
$ErrorActionPreference = 'Stop'
$client = [System.Net.Sockets.TcpClient]::new()
$client.Connect('127.0.0.1', $Port)
$stream = $client.GetStream()
$stream.ReadTimeout = 45000
$stream.WriteTimeout = 45000
function Read-Exact([int]$count) {
    $buffer = [byte[]]::new($count)
    $offset = 0
    while ($offset -lt $count) {
        $read = $stream.Read($buffer, $offset, $count - $offset)
        if ($read -eq 0) { throw 'Unity closed the connection.' }
        $offset += $read
    }
    return ,$buffer
}
try {
    do { $character = $stream.ReadByte() } while ($character -ge 0 -and $character -ne 10)
    $payload = [System.Text.Encoding]::UTF8.GetBytes((@{ type = $Type; params = ($Parameters | ConvertFrom-Json) } | ConvertTo-Json -Depth 15 -Compress))
    $header = [BitConverter]::GetBytes([long]$payload.Length)
    if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($header) }
    $stream.Write($header, 0, 8)
    $stream.Write($payload, 0, $payload.Length)
    $replyHeader = Read-Exact 8
    if ([BitConverter]::IsLittleEndian) { [Array]::Reverse($replyHeader) }
    $length = [BitConverter]::ToInt64($replyHeader)
    if ($length -gt 16000000) { throw 'Unexpected Unity response length.' }
    [System.Text.Encoding]::UTF8.GetString((Read-Exact $length))
} finally { $client.Dispose() }
