# Timelog

## Abstract
Timelog is a solution created with the purpose of measure execution time of distributed transactions.
Timelog uses 3 main blocks:
- Timelog client: Is to be used by any service that we need to measure executions time, it will be sending logs to the server.
- Timelog server: Will receive the logs and stamp them with a unique Timestamp, in order to have the same time reference between all the applications or services participating in a transaction.
- Timelog viewer: Will be used to view and query the server logs.

**Critical Success Factors (CSF)'s that must always be guiding any developments of this project:**
- Measure execution time of any module
- Timelog must be fast developed
- Timelog integration must be easy and have a fast adoption
- Timelog needs to be lightning-fast;
- Timelog source code should be kept at the minimum;

**Compromises:**
- Timelog security policy will be kept at a basic level;
- Timelog resilience can be kept at minimum - server cannot stop functioning but it is acceptable to lose some messages

**Decisions:**
- Communications between the Timelog client and the server are made over UDP to enhance logging performance;
- Communications between the Timelog viewer and the server are made over TCP;
- Timelog server must record the logs sent by the client in memory first to enhance performance;
- Timelog will be publicly available as an open source project;
- All Date Time shall be measured in UTC

## Timelog client specification

### Configuration
**1. Application Key**
- Type: GUID
- Description: A unique identifier of the client. Used in the server to identify the client application in the authorization process and in the listener management process.

**2. Timelog Server Host**
- Type: String
- Description: The FQDN of the Timelog server or their IP address.

**3. Timelog Server Port**
- Type: int
- Description: The Timelog server network port number, that will be open to receive the time messages sent by the client.

### Methods
#### Startup  
Startup should load the configuration and instanciate a single UDP client.

#### Log  
Log method should receive the following inputs, convert the message into binary and use the UDP single instance to send it to Timelog server.
Failed sent's should not raise any exception.
Inputs:
 - LogLevel  
 Type: enum with the values Trace, Debug, Information, Warning, Error  
 Description: Log severity level  
 - Message  
 Type: string  
 Description: Message to be logged  

#### Dispose
Dispose the UDP single instance.




## Timelog server specification

### Configuration
**1. Authorizations file path**  
- Type: String  
- Description: The path of a json file with the Access Control List (ACL), of the clients that will be authorized to communicate with the server.

**2. Timelog Server Port**  
- Type: int  
- Description: The Timelog server network port number, that will be open to receive the logs sent by the client.

**3. Internal Cache Max Entries**  
- Type: int  
- Description: The number of entries to be accepted on the global internal cache

**4. Maximum number viewers**  
- Type: int  
- Description: The maximum number of viewers at the same time

**5. Maximum Log Files**  
- Type: int  
- Description: The maximum number of log files to be created. Log files are rotating.

**6. Maximum Log File entries**  
- Type: int  
- Description: The maximum number of entries per log file. When the maximum entries is reached a new log file will be written.

### Methods
#### Startup
During Startup the UDP port shall be opened, the ACL read and loaded into memory, the internal cache and the file queues setup

#### Receive TimeLogs
When a TimeLog package arrives within the UDP port the following process is to be executed:
 1. Parse UDP package into a strongly type instance of TimeLog, if invalid package, discard the TimeLog
 2. Stamp TimeLog with Server UTCNow date time
 3. Check source APIKey against loaded ACL - if authorization check fails, discard the TimeLog
 4. Save the log entry in the internal cache
 5. For each viewer check if this log entry has a match with the viewer search criteria and mark it with a bit
 5. Listeners of new TimeLog internal event will process the TimeLog accordingly to their implementation:
	<ol style="list-style-type: lower-alpha;">
    <li>Internal Cache with the last X* messages</li>
    <li>File queues will append TimeLog to their current queue</li>
    <li>Specific listeners will append TimeLog to their cache if it passes the search filter</li>
	</ol>

_*X configured cache size_

#### Connect/Disconnect Viewers 
On Connect - the viewer API key is checked against the ACL <u>json</u>, and discarded if invalid <u>(The ACL will be in JSON format to ensure that no incompatibilities exist)</u> .A new viewer instance will be loaded with the received search filter; this search filter will contain the following parameters:
  
**RealTime**, bool - whether or not the listener is in real time mode  
**RealTimeInterval**, nullable int - real-time window to use when in real time  
**StartTime**, nullable DateTime, the beginning of the time interval the listener is interested in filtering, used when not in RealTime  
**EndTime**, nullable DateTime, the end of the time interval the listener is interested in filtering, used when not in RealTime  
**APIKey**, the value of the APIKey the listener is interested in filtering  
**Hierarchy<u>Mask</u>**, the mask to be use to filter the hierarchy field - it will use the exact same concept as an IPV4 network mask  
**TransactionId**, the value of a single transactionId to filter the TimeLog messages

Each viewer will be kept in a viewers internal cache with the following properties:
- FirstConnectTime
- Filter
- LastCheckTime (updated with the last viewer check time)
- NextCheckTime ( Is the LastCheckTime+RealTimeInterval, which is the expected time of the viewer next check, if it doesn't happen we may kill this viewer from the viewers internal cache)

??After setup the listener internal Cache will load the top X TimeLog that pass the filter. It will start by the most recent time logs that are in the cache and, if the filter allows and the cache does not fill the max TimeLogs to fill the listener cache, start searching in the file logs
When ready it will wire up the internal new TimeLog event

When a new TimeLog is received on the internal new TimeLog event, the listener will:

 1. Check if the new TimeLog passes the listener filter, if not the TimeLog is discarded
 2. Append the TimeLog to the internal listener cache and stamp it with the next index number
 3. Assure the internal listener cache is below the configured limit

On Disconnect - the viewer disconnects and is removed from the viewers internal cache.

### Sending Listener TimeLog ###  
When the listener customer requires an update for the existing TimeLogs it will send an integer that represents the first index number (inclusive) it's interested in.
The specific listener will then return all messages where the index is greater or equal than the received index
The listener shall keep track of the moment this method was invoked to be used in the zombie check process

## Processes  
Some internal processes are launched during startup and have the responsibility of keeping the time server running for some specific features.
These methods will be triggered accordingly to some configured frequency.

### File Log writting ###  
When executed will:  
 1. Switch the current File Queue Index
 2. Dump the content of the previous File Queue to the TimeLog file, update the first 2 lines accordingly with the beginning and ending time entries of the file.
 3. Clear the previous File Queue

The file log rotating should happen when one of the following conditions are met:
- the maximum log entries are reached
- the maximum time limit is reached

### Kill zombie listeners ###  
When executed will:  
 1. Check all registered listeners and look for those where the last interaction occurred before a given configuration. Those where the last interaction was above the max allowed time are considered zombies and will disconnected as if their Disconnect method was invoked.

## Notes

**Cache implementation**  
Caches will be implemented as a RoundRobinList<TimeLogEntry>. This implementation will keep track internally of the current index and automatically override older values when the max size of the cache is reached

**Thread Safe Considerations**  
All Cache write methods should be wrapped inside a ReadWriterLockSlim write lock and all read methods should be wrapped inside the same ReadWriterLockSlim read lock. Preferably this will be implementated within the RoundRobinList implementation

**Time Log File storage**  
The number of log files to create is fixed by configuration, so each log file name shall be:
timelog_XXX.log
Where XXX is the padded zero index number of the file log
When searching for a specific time interval it should be simple to obtain the log file pointers of those log files that contain entries within the interval by inspecting <u>the first 2 lines of the file. These 2 lines are the start and end time of the log entries recorded in this log file. </u>
An internal table shall be kept that will hold the LogFile pointer and the first and last entry in each LogFile.
Data inside the LogFile is exactly the same as data inside the Cache where each entry is the binary representation of a TimeLog entry. This will allow quick loading of a TimeLog file into a searchable structure to streamline and speed up the listener initialization.

**API Keys**  
Since the API Keys are managed by us, and in order to improve the `CompareTo` performance, we should choose keys where the last 4 bytes are always different among the existing ones.  

## Timelog Viewer Specification

The Timelog viewer serves the purpose of querying and visualizing logs received by the Timelog server. It can specify search parameters such as "APIKey," "Hierarchy," and "TransactionId." The viewer provides two main functionalities: 

1. **Real-Time Search:** 
The viewer allows for real-time searches considering a specific time window (e.g., 5/10/30/60 mins) from the current moment. The results of this search provide insights into recent transactions and execution times.

<div style="text-align: center;">
<img src="https://i.imgur.com/RdhJ8pk.jpg" alt="Alt Text" width="40%">

Image 1: Viewer for Real-Time Search
</div>


2. **Search by Specific Time Interval:** 
The viewer supports searching logs within a specific interval defined by a "From" timestamp and a "To" timestamp. This functionality enables users to explore logs from a historical perspective.

<div style="text-align: center;">
<img src="https://i.imgur.com/Cg39TF2.png" alt="Alt Text" width="40%">

Image 2: Viewer for Search by Specific Time Interval
</div>

## Timelog Implementation Steps

### Step 1: UDP Server for Receiving Logs 
1. **Set up one port for the UDP listener**  to receive packets. Each packet adheres to the specified structured format (Specified in Appendices -> Timelog message specification -> Message Format). 
2. **Parse the received packets**  and extract the fields: application key, domain, command, transactionID, original timestamp, reserved bytes, variable extent, and the extent bytes. 
3. **Insert the parsed data into the MainCache** , a round-robin cache with a a fixed size of records. Each record will also include the current server timestamp and a sequential number (SeqNumber).

### Step 2: Round Robin Cache Implementation
Implement a round-robin cache `MainCache` for storing logs with a fixed size to avoid memory overflow. This can be achieved using a fixed-size array and an index to keep track of the oldest entry for replacement.

### Step 3: Socket Server for Viewers Communications 
1. **Set up one port for the socket server**  for two-way communication with viewers. This server listens for special packets from viewers requesting logs. 
2. **Parse viewers requests**  to extract the application key, domain mask, start time, end time, and transaction ID. 
3. **Filter logs from MainCache**  based on the search criteria, including application key matching and domain masking similar to TCP/IP subnet masking. 
4. **Store the filtered logs in a ViewerXCache**  specific for the requesting viewer, and track the lowest and highest SeqNumber.

### Step 4: Domain Mask Filtering
Implement a function to filter domains based on a mask, similar to subnet masking in TCP/IP. This involves bitwise operations to compare the domain against the mask.

### Step 5: Sending Data to Viewers 
1. **Group filtered logs into packets** of a fixed size of records and send them to the requesting viewer through the established socket connection. 
2. **Update a watermark register**  with the highest timestamp included in each sent packet.



## Appendices

### Timelog message specification

#### Message Format
- Each Timelog message consist's of header and data.
- The header contains metadata about the data.
- The data contains the actual content being transmitted.
- The size of the message (header + data) should not exceed 1500 bytes (this is the default MTU, so if it's exceeded the packet could be dropped or fragmented by the network layer)


#### Header Format:

<div style="text-align: center;">
<img src="https://i.imgur.com/BFMz6HI.png" alt="Alt Text" width="100%">

Image 3: Header Format
</div>

**1. ApplicationKey (16 bytes):**  
Type: GUID  
Description: A unique identifier of the Client application to identify/authorize where the data comes from.  

**2. Domain (4 bytes):**  
Type: String  
Description: A value representing the hierarchy level of the message, it will be hold as an IP address to use the same kind of filtering as used in networking with network masks  

**3. TransactionID (64 bytes):**  
Type: String  
Description: The unique identifier of a transaction associated with the data.  

**4. Command (4 bytes):**  
Type: Int  
Description: A command associated with the TimeLog Entry:  
 0 - None  
 1 - Start  
 2 - Stop  
 3 - Wait  

**5. OriginTimestamp (8 bytes):**  
Type: long (8 bytes)  
Description: UTC DateTime ticks in the source system when the message was created.  

**6. TimeServerTimeStamp (8 bytes):**  
Type: long (8 bytes)  
Description UTC Timestap placed by the TimeServer when a message arrives  

**7. Reserved (128 bytes):**  
Type: Undefined  
Description: Space reserved for future controls or additional metadata.  

**8. DataHeader (128 bytes):**  
Type (String)  
Description: Used to hold data metadata and describe the expected data format  

**9. Data: (1024 bytes):**  
Data (1024 bytes maximum):  
Type: Byte array  
Description: Actual log content, in UTF8 and converted to bytes 



## Doubts
- "Include the packet version" (GC). Where, in the message or in the search criterias?

- Keys: 
1. Authentication Client Key (App Key)
2. Api_Key
Na nossa cache guardamos a Api_Key

Ex:
A Unick pode ter 12 clientes diferentes com 12 Api_keys diferentes. Mas o servidor sabe que todas correspondem à Unick:

1 Api Key -> dá para -> 1 App Key
1 App Key -> dá para -> N Api Keys

- Excel como Viewer (GC).
- Web em vez de Windows Form de onde se saca diretamente o excel (GC).