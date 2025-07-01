# High-Throughput Payment Gateway

## Project Overview

This project is a backend system designed to simulate a high-performance payment gateway. It is architected to handle a large volume of transactions asynchronously, ensuring system resilience and scalability. The core concept is to accept payment requests, process them in the background, and notify client systems of the outcome via webhooks.

This project focuses exclusively on backend architecture, performance, and reliability, demonstrating advanced concepts relevant to the FinTech and high-transaction-volume industries.

## Technical Skills Showcase

This project was specifically built to demonstrate expertise in designing and building resilient, high-throughput distributed systems.

* **Core Backend**: C#, .NET 8, ASP.NET Core Web API, Worker Services
* **Architecture & Patterns**: Microservices, Event-Driven Architecture, CQRS, Asynchronous Processing, Resiliency Patterns (Retry, Circuit Breaker)
* **High-Throughput Messaging**: Apache Kafka for event streaming, capable of handling millions of messages per second
* **Polyglot Persistence**:

  * **Redis**: For high-speed, in-memory caching (e.g., API key validation)
  * **MongoDB**: For fast, scalable, unstructured data storage (e.g., transaction logs)
* **System Resilience**: Polly for implementing robust Retry and Circuit Breaker patterns, ensuring the system can handle transient failures from external services (like a bank API)
* **Containerization**: The entire application and its infrastructure (Kafka, Zookeeper, Redis, MongoDB) are fully containerized with Docker and orchestrated locally with Docker Compose

## System Architecture

The system consists of several focused microservices that communicate primarily through Kafka, ensuring they are fully decoupled.

**Architecture Flow:**

1. **Client System**: Web shop or other consumer system sends a payment request
2. **PaymentAPI**: Accepts requests, validates API key via Redis, publishes event
3. **Kafka**: Distributes events to processing services
4. **PaymentProcessor**: Processes payments, handles retries, logs to MongoDB, publishes outcome
5. **WebhookSender**: Notifies client via the provided webhook URL

## Detailed Workflows

### 1. Payment Request Submission (Asynchronous Acceptance)

* A client system (e.g., a web shop) sends a POST request to `/api/payments` on the Payment API.
* The request includes payment details and a `webhookUrl`. An API key is provided in the header.
* The Payment API performs high-speed validation by checking the API key against Redis.
* If valid, it immediately publishes a `PaymentRequestReceived` event to a Kafka topic.
* It returns an HTTP `202 Accepted` response with a unique transaction ID.
* The API does **not** wait for processing to complete.

### 2. Payment Processing (Background & Resilient)

* The Payment Processor service, a Kafka consumer, picks up the `PaymentRequestReceived` event.
* It simulates communication with an external bank API using Polly with retry and circuit breaker policies.
* If the bank API is down, Polly ensures resilience by retrying or temporarily stopping requests.
* A transaction log is stored in MongoDB.
* A `PaymentProcessed` event (with Success or Failure) is published to Kafka.

### 3. Webhook Notification

* The Webhook Sender service consumes the `PaymentProcessed` event.
* It sends a POST request to the `webhookUrl` originally provided, including the transaction outcome.

## How to Run Locally

### Prerequisites

* [.NET 8 SDK](https://dotnet.microsoft.com/download)
* [Docker Desktop](https://www.docker.com/products/docker-desktop)

### Run the Infrastructure

In the project root, run Docker Compose to start all necessary services:

```bash
docker-compose up -d
```

This starts:

* Kafka
* Zookeeper
* Redis
* MongoDB

### Run the Application

1. Open the solution in **Visual Studio**.
2. Set multiple startup projects:

   * `PaymentAPI`
   * `PaymentProcessor`
   * `WebhookSender`
3. Press `F5` to run.

### Test the Flow

1. Set a test API key in Redis:

```bash
docker exec -it redis redis-cli
SET apikeys:test-key-123 "Test Web Shop"
```

2. Send a POST request to the Payment API using Postman, Swagger, or curl. Include the API key and webhook URL.

3. Use **Kafdrop** (available at [http://localhost:19000](http://localhost:19000)) to inspect Kafka topics.

4. Use [webhook.site](https://webhook.site) to receive the notification from WebhookSender.
