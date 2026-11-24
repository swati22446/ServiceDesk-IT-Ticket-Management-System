namespace ServiceDesk.Api.Common;

/// <summary>Requested resource does not exist (HTTP 404).</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>User is authenticated but not allowed to do this (HTTP 403).</summary>
public class ForbiddenException(string message) : Exception(message);

/// <summary>A business rule was violated (HTTP 400).</summary>
public class BusinessRuleException(string message) : Exception(message);

/// <summary>Login failed (HTTP 401).</summary>
public class AuthenticationFailedException(string message) : Exception(message);
