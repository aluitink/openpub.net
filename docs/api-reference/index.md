# API Reference

## Overview

This document provides API documentation for the ActivityPub.Core library, organized by feature area following the new directory structure.

## Core Models

### Activity Types

- **Activity** - Base activity stream activity
- **Create** - Create activity for posting new content
- **Delete** - Delete activity for removing content
- **Update** - Update activity for modifying existing content
- **Follow** - Follow activity for establishing social connections
- **Accept** - Accept activity for accepting follow requests
- **Reject** - Reject activity for declining follow requests
- **Like** - Like activity for expressing approval
- **Announce** - Announce activity for sharing content (boost/retweet)
- **Undo** - Undo activity for reversing previous activities

### Actor Types

- **Actor** - Base actor type
- **Person** - Individual user actor
- **Application** - Application actor
- **Group** - Group actor
- **Organization** - Organization actor
- **Service** - Service actor

### Collection Types

- **Collection** - Basic activitypub collection
- **CollectionPage** - Paginated collection page
- **OrderedCollection** - Ordered collection
- **OrderedCollectionPage** - Paginated ordered collection
- **CollectionItem** - Individual collection items

### WebFinger Models

- **WebFingerResponse** - WebFinger JRD response
- **WebFingerLink** - WebFinger link entries
- **WebFingerJrd** - JSON Resource Descriptor

## Services

### Activity Processing

- **InboxProcessorService** - Processes incoming activities
- **ActivityValidationService** - Validates activity structure
- **OutboundActivityService** - Sends outbound activities

### Federation

- **FederationDiscoveryService** - Discovers remote servers
- **SharedInboxService** - Handles shared inbox deliveries
- **WebSubService** - Manages WebSub subscriptions

### WebFinger

- **WebFingerCacheService** - Caches WebFinger responses
- **DefaultWebFingerSource** - Default WebFinger resolution

### Security

- **OutboundSigningService** - Signs outbound requests
- **KeyFetchingService** - Fetches actor public keys
- **KeyGenerationService** - Generates actor key pairs

### Background

- **SharedInboxBackgroundService** - Background queue processor

## Controllers

### API Controllers

- **ActorController** - Actor management endpoints
- **WebFingerController** - WebFinger resolution endpoints
- **HealthController** - Health check endpoints

### API Versioning

- **WebFingerVersionedController** - Versioned WebFinger API

## Infrastructure

### Data

- **ActivityPubDbContext** - EF Core database context
- **ActivityEntity** - Database entity for activities
- **ActorEntity** - Database entity for actors

### Repositories

- **IActivityPubRepository** - Repository interface
- **EFCoreActivityPubRepository** - EF Core repository implementation
- **InMemoryActivityPubRepository** - In-memory repository for testing

### Caching

- **IFederationCache** - Federation cache interface
- **MemoryFederationCache** - In-memory cache implementation
- **CacheInvalidationService** - Cache invalidation service

## Options

- **ActivityPubOptions** - Configuration options

## Middleware

- **HttpSignatureMiddleware** - HTTP signature verification
- **SigningVerificationMiddleware** - Request signing verification
- **RateLimitingMiddleware** - Rate limiting

## Events

- **IActivityPubEvent** - Event interface
- **ActivityPubEvent** - Event implementation
- **IActivityPubEventHandler** - Event handler interface
- **IActivityPubInterceptor** - Interceptor interface

## Plugins

- **IActivityPubPlugin** - Plugin interface
- **PluginManager** - Plugin management
- **PluginRegistry** - Plugin registry

## Implementations

- **InboxProcessor** - Default inbox processor
- **SampleActivityPubInterceptor** - Sample interceptor

## Extension Methods

- **ActivityPubServiceCollectionExtensions** - Service collection extensions
