# Image Resizer (Azure Functions POC using C# & .NET Core)

Proof-of-concept Azure Function that consumes a base64-encoded image from a Service Bus queue, resizes it to several variants, and uploads JPEG blobs to Azure Blob Storage.

Typical use case: an upload pipeline enqueues raw image data; this function writes `original`, `large`, and `small` blobs under a new GUID prefix.

## Flow

```
Service Bus queue
        │
        ▼
ResizeAndStore function
        │
        ├── upload {guid}_original.jpg  (scale 1.0)
        ├── upload {guid}_large.jpg     (scale 2.0 in code — see note below)
        └── upload {guid}_small.jpg     (scale 0.25)
        │
        ▼
Blob container
```

Connection strings and queue name are read from app settings (`QueueConnectionString`, `BlobStorageConnectionString`).
