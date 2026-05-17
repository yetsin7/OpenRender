using Silk.NET.OpenGL;

namespace OpenRender.Rendering.Shaders;

/// <summary>
/// Compiles and manages an OpenGL shader program from vertex and fragment shader sources.
/// Provides methods for setting uniform values.
/// </summary>
public class ShaderProgram : IDisposable
{
    private readonly GL _gl;
    private readonly uint _handle;
    private readonly Dictionary<string, int> _uniformLocations = new();
    private bool _disposed;

    /// <summary>
    /// Creates and compiles a shader program from vertex and fragment shader source code.
    /// </summary>
    public ShaderProgram(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;

        uint vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
        uint fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

        _handle = _gl.CreateProgram();
        _gl.AttachShader(_handle, vertexShader);
        _gl.AttachShader(_handle, fragmentShader);
        _gl.LinkProgram(_handle);

        _gl.GetProgram(_handle, ProgramPropertyARB.LinkStatus, out int status);
        if (status == 0)
        {
            string infoLog = _gl.GetProgramInfoLog(_handle);
            throw new Exception($"Shader program link error: {infoLog}");
        }

        _gl.DetachShader(_handle, vertexShader);
        _gl.DetachShader(_handle, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);
    }

    /// <summary>
    /// Activates this shader program for rendering.
    /// </summary>
    public void Use()
    {
        _gl.UseProgram(_handle);
    }

    /// <summary>
    /// Gets the uniform location, caching it for performance.
    /// </summary>
    public int GetUniformLocation(string name)
    {
        if (_uniformLocations.TryGetValue(name, out int location))
            return location;

        location = _gl.GetUniformLocation(_handle, name);
        _uniformLocations[name] = location;
        return location;
    }

    /// <summary>
    /// Sets a float uniform value.
    /// </summary>
    public void SetFloat(string name, float value)
    {
        _gl.Uniform1(GetUniformLocation(name), value);
    }

    /// <summary>
    /// Sets an int uniform value.
    /// </summary>
    public void SetInt(string name, int value)
    {
        _gl.Uniform1(GetUniformLocation(name), value);
    }

    /// <summary>
    /// Sets a vec3 uniform value.
    /// </summary>
    public void SetVec3(string name, float x, float y, float z)
    {
        _gl.Uniform3(GetUniformLocation(name), x, y, z);
    }

    /// <summary>
    /// Sets a vec3 uniform from a System.Numerics.Vector3.
    /// </summary>
    public void SetVec3(string name, System.Numerics.Vector3 v)
    {
        _gl.Uniform3(GetUniformLocation(name), v.X, v.Y, v.Z);
    }

    /// <summary>
    /// Sets a mat4 uniform value.
    /// </summary>
    public unsafe void SetMat4(string name, System.Numerics.Matrix4x4 matrix)
    {
        _gl.UniformMatrix4(GetUniformLocation(name), 1, false, (float*)&matrix);
    }

    /// <summary>
    /// Sets a mat3 uniform value.
    /// </summary>
    public unsafe void SetMat3(string name, System.Numerics.Matrix4x4 matrix)
    {
        // Extract 3x3 from 4x4
        float* mat3 = stackalloc float[9];
        mat3[0] = matrix.M11; mat3[1] = matrix.M12; mat3[2] = matrix.M13;
        mat3[3] = matrix.M21; mat3[4] = matrix.M22; mat3[5] = matrix.M23;
        mat3[6] = matrix.M31; mat3[7] = matrix.M32; mat3[8] = matrix.M33;
        _gl.UniformMatrix3(GetUniformLocation(name), 1, false, mat3);
    }

    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        
        // Clean source: ensure it has no BOM or junk
        string cleanSource = source.Replace("\r", "").Trim().Trim('\uFEFF', '\u200B');
        
        // Strip existing #version directive if present so we can try our fallbacks
        if (cleanSource.StartsWith("#version"))
        {
            int nextLine = cleanSource.IndexOf('\n');
            if (nextLine != -1)
            {
                cleanSource = cleanSource.Substring(nextLine + 1).TrimStart();
            }
        }
        
        // Try multiple version directives
        string[] versions = { "#version 330 core", "#version 330", "#version 300 es", "#version 150" };
        string lastError = "";

        foreach (var v in versions)
        {
            string finalSource = v + "\n" + (v.Contains("es") ? "precision highp float;\n" : "") + cleanSource;
            _gl.ShaderSource(shader, finalSource);
            _gl.CompileShader(shader);
            
            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status != 0) return shader;

            lastError = _gl.GetShaderInfoLog(shader);
        }

        throw new Exception($"Shader compilation error ({type}): {lastError}");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _gl.DeleteProgram(_handle);
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
