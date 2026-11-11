using EmployeeManagementSystem.DTOs.Employee;
using EmployeeManagementSystem.Models;
using MongoDB.Driver;

namespace EmployeeManagementSystem.Services
{
    public class EmployeeService
    {
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<Department> _departments;
        private readonly ILogger<EmployeeService> _logger;

        public EmployeeService(IMongoDatabase database, ILogger<EmployeeService> logger)
        {
            _users = database.GetCollection<User>("users");
            _departments = database.GetCollection<Department>("departments");
            _logger = logger;
        }

        // Get all employees
        //public async Task<List<EmployeeResponseDto>> GetAllEmployeesAsync(
        //    CancellationToken cancellationToken = default)
        //{
        //    _logger.LogInformation("Fetching all active employees.");

        //    var filter = Builders<User>.Filter.Eq(
        //        user => user.DeletedAt, null);

        //    var users = await _users
        //        .Find(filter)
        //        .ToListAsync(cancellationToken);

        //    return users.Select(MapToResponse).ToList();
        //}

        public async Task<List<EmployeeResponseDto>> GetAllEmployeesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching all active employees.");

            var employees = await _users
                .Find(user => user.DeletedAt == null)
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully fetched {EmployeeCount} active employees.",
                employees.Count);

            return employees.Select(MapToResponse).ToList();
        }

        // Get employee by ID
        //public async Task<EmployeeResponseDto?> GetEmployeeByIdAsync(
        //    string id,
        //    CancellationToken cancellationToken = default)
        //{
        //    if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
        //    {
        //        return null;
        //    }

        //    var filter = Builders<User>.Filter.And(
        //        Builders<User>.Filter.Eq(user => user.Id, id),
        //        Builders<User>.Filter.Eq(user => user.DeletedAt, null)
        //    );

        //    var user = await _users
        //        .Find(filter)
        //        .FirstOrDefaultAsync(cancellationToken);

        //    return user == null ? null : MapToResponse(user);
        //}

        public async Task<EmployeeResponseDto?> GetEmployeeByIdAsync(string id,CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Fetching employee with ID: {EmployeeId}", id);

            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            {
                _logger.LogWarning(
                    "Invalid employee ID format: {EmployeeId}", id);

                return null;
            }

            var employee = await _users
                .Find(user => user.Id == id && user.DeletedAt == null)
                .FirstOrDefaultAsync(cancellationToken);

            if (employee == null)
            {
                _logger.LogWarning(
                    "Employee not found with ID: {EmployeeId}", id);

                return null;
            }

            _logger.LogInformation(
                "Successfully retrieved employee {EmployeeCode}",
                employee.EmployeeCode);

            return MapToResponse(employee);
        }

        //Create a new employee
        public async Task<EmployeeResponseDto> CreateEmployeeAsync(
            CreateEmployeeDto dto,
            CancellationToken cancellationToken = default)
        {

            try
            {
                _logger.LogInformation(
                    "Creating employee with email: {Email}", dto.Email);

                // Keep your existing CreateEmployeeAsync logic here.


                // 1. Check whether EmployeeCode already exists
                var employeeCodeExists = await _users
                    .Find(user => user.EmployeeCode == dto.EmployeeCode)
                    .AnyAsync(cancellationToken);

                if (employeeCodeExists)
                {
                    throw new InvalidOperationException(
                        "An employee with this EmployeeCode already exists.");
                }

                // 2. Check whether Email already exists
                var emailExists = await _users
                    .Find(user => user.Email == dto.Email)
                    .AnyAsync(cancellationToken);

                if (emailExists)
                {
                    throw new InvalidOperationException(
                        "An employee with this Email already exists.");
                }

                // 3. Verify DepartmentId
                var department = await _departments
                    .Find(dept => dept.Id == dto.DepartmentId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (department == null)
                {
                    throw new InvalidOperationException(
                        "The specified department does not exist.");
                }

                // 4. Hash the password
                string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

                // 5. Create the User document
                var user = new User
                {
                    Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                    EmployeeCode = dto.EmployeeCode,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    DisplayName = $"{dto.FirstName} {dto.LastName}",
                    Email = dto.Email,
                    Phone = dto.Phone ?? string.Empty,
                    PasswordHash = passwordHash,
                    Role = dto.Role,
                    Status = dto.Status,
                    DepartmentId = dto.DepartmentId,
                    ManagerId = dto.ManagerId,

                    Profile = new UserProfile
                    {
                        JobTitle = dto.JobTitle ?? string.Empty
                    },

                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Version = 1
                };

                // 6. Insert document into MongoDB
                await _users.InsertOneAsync(
                    user,
                    cancellationToken: cancellationToken);

                // 7. Return response DTO
                //return MapToResponse(user);


                // After successful insertion:
                _logger.LogInformation(
                    "Employee created successfully. EmployeeCode: {EmployeeCode}",
                    user.EmployeeCode);

                return MapToResponse(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred while creating employee with email: {Email}",
                    dto.Email);

                throw;
            }

        }


        //Update an existing employee
        public async Task<EmployeeResponseDto?> UpdateEmployeeAsync(
            string id,
            UpdateEmployeeDto dto,
            CancellationToken cancellationToken = default)
        {

            _logger.LogInformation("Updating employee with ID: {EmployeeId}", id);
            // 1. Validate MongoDB ObjectId
            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            {
                return null;
            }

            // 2. Find the existing employee
            var filter = Builders<User>.Filter.And(
                Builders<User>.Filter.Eq(user => user.Id, id),
                Builders<User>.Filter.Eq(user => user.DeletedAt, null)
            );

            var user = await _users
                .Find(filter)
                .FirstOrDefaultAsync(cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Employee with ID: {EmployeeId} not found.", id);
                return null;
            }

            // 3. Check whether the email belongs to another employee
            var emailExists = await _users
                .Find(u => u.Email == dto.Email && u.Id != id)
                .AnyAsync(cancellationToken);

            if (emailExists)
            {
                _logger.LogWarning("Email address {Email} is already in use.", dto.Email);
                throw new InvalidOperationException(
                    "This email address is already used by another employee.");

            }

            // 4. Verify the department exists
            var department = await _departments
                .Find(dept => dept.Id == dto.DepartmentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (department == null)
            {
                throw new InvalidOperationException(
                    "The specified department does not exist.");
            }

            // 5. Update employee fields
            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.DisplayName = $"{dto.FirstName} {dto.LastName}";
            user.Email = dto.Email;
            user.Phone = dto.Phone ?? string.Empty;
            user.Role = dto.Role;
            user.Status = dto.Status;
            user.DepartmentId = dto.DepartmentId;
            user.ManagerId = dto.ManagerId;

            // Update job title without replacing other profile information
            user.Profile ??= new UserProfile();
            user.Profile.JobTitle = dto.JobTitle ?? string.Empty;

            // 6. Update modification details
            user.UpdatedAt = DateTime.UtcNow;
            user.Version++;

            // 7. Save changes to MongoDB
            await _users.ReplaceOneAsync(
                filter,
                user,
                cancellationToken: cancellationToken);


            _logger.LogInformation("Employee updated successfully. EmployeeId: {EmployeeId}", id);

            // 8. Return updated employee
            return MapToResponse(user);
        }

        // Delete Employee
        public async Task<bool> DeleteEmployeeAsync(
            string id,
            CancellationToken cancellationToken = default)
        {

            _logger.LogInformation("Deleting employee with ID: {EmployeeId}", id);
            // 1. Validate MongoDB ObjectId
            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
            {
                _logger.LogWarning("Invalid employee ID format: {EmployeeId}", id);
                return false;
            }

            // 2. Find an active employee
            var filter = Builders<User>.Filter.And(
                Builders<User>.Filter.Eq(user => user.Id, id),
                Builders<User>.Filter.Eq(user => user.DeletedAt, null)
            );

            // 3. Prepare soft-delete updates
            var update = Builders<User>.Update
                .Set(user => user.DeletedAt, DateTime.UtcNow)
                .Set(user => user.UpdatedAt, DateTime.UtcNow)
                .Inc(user => user.Version, 1);

            // 4. Update the document
            var result = await _users.UpdateOneAsync(
                filter,
                update,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Employee soft-deleted successfully. EmployeeId: {EmployeeId}", id);

            // 5. Return whether an employee was found and updated
            return result.ModifiedCount > 0;
        }




        // Map MongoDB User model to API response DTO
        private static EmployeeResponseDto MapToResponse(User user)
        {
            return new EmployeeResponseDto
            {
                Id = user.Id,
                EmployeeCode = user.EmployeeCode,
                FirstName = user.FirstName,
                LastName = user.LastName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                Phone = user.Phone,
                Role = user.Role,
                Status = user.Status,
                DepartmentId = user.DepartmentId,
                JobTitle = user.Profile?.JobTitle ?? string.Empty,
                CreatedAt = user.CreatedAt
            };
        }
    }
}
