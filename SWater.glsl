#define EPSILON .0001
#define MAX_STEPS 128


#define M_PI 3.14159265358979

uniform vec3 iCameraPosition;
uniform float HalfSizePool=3.0;
uniform float DepthPool = 3;

uniform float BallSize = 0.75;
uniform vec3 LightPos = vec3(2.,1.5,0);
uniform vec3 WaterNumber = vec3(0.4,0.9,1);

vec3 LightSource = LightPos;

vec3 WaterColor = WaterNumber;

const float WaterHeight = 0.0;
const float MaxWaveAmplitude = 0.04;

const float HeightPool = 1;
//float HalfSizePool = 5;
//const float DepthPool = 3;

struct MaterialInfo {
	vec3 Kd;
	float Shininess;
};

float cyclicTime = mod(iGlobalTime, 30);

float WaveAmplitude() {
	return MaxWaveAmplitude * exp(-cyclicTime / 10);
}

float WaterWave(vec3 a) {
	return WaveAmplitude() * sin((2 * a.x * a.x + 2 * a.z * a.z) - 10 * cyclicTime);
}

float BallOscillation() {
	return sin(5 * cyclicTime + 4) * exp(-cyclicTime / 6) + .3;
}

float PoolBottom(vec3 a) {
	return a.y + DepthPool + .01;
}

float BackWall(vec3 a) {
	return a.z + HalfSizePool + .01;
}

float LeftWall(vec3 a) {
	return a.x + HalfSizePool + .01;
}

float WaterSurface(vec3 a) {
	vec3 sz = vec3(HalfSizePool, 0, HalfSizePool);
	return length(max(abs(a + vec3(0, WaterWave(a), 0)) - sz, 0.));
}

float Pool(vec3 a) {
	return min(PoolBottom(a), min(LeftWall(a), BackWall(a)));
}

float Ball(vec3 a) {
	return length(a + vec3(0., BallOscillation(), 0.)) - BallSize;
}

float Scene(vec3 a) {
	return min(WaterSurface(a), min(Ball(a), Pool(a)));
}

bool IsWaterSurface(vec3 a) {
	float closest = Ball(a);
	float sample = Pool(a);
	if (sample < closest) {
		closest = sample;
	}	
	sample = WaterSurface(a);
	if (sample < closest) {
		return true;
	}
	return false;
}

bool IsWater(vec3 pos ){
	return (pos.y < (WaterHeight - MaxWaveAmplitude));
}

vec3 PoolColor(vec3 pos) {		
	if ((pos.y > HeightPool) || (pos.x > HalfSizePool) || (pos.z > HalfSizePool)) 
		return vec3(0.0);
	float tileSize = 0.2;
	float thickness = 0.015;
	vec3 thick = mod(pos, tileSize);
	if ((thick.x > 0) && (thick.x < thickness) || (thick.y > 0) && (thick.y < thickness) || (thick.z > 0) && (thick.z < thickness))
		return vec3(1);
	return vec3(sin(floor((pos.x + 1) / tileSize)) * cos(floor((pos.y + 1) / tileSize)) * sin(floor((pos.z + 1) / tileSize)) + 3);
}

// material for specified point
MaterialInfo Material(vec3 a) {
	MaterialInfo m = MaterialInfo(vec3(.5, .56, 1.), 50.);
	//MaterialInfo m = MaterialInfo(vec3(2, 2, 2), 200.);
	float closest = Ball(a);

	float sample = WaterSurface(a);
	if (sample < closest) {
		closest = sample;
		m.Kd = WaterColor;
		m.Shininess = 120;
	}
	// second scene object
	sample = Pool(a);
	if (sample < closest) {
		closest = sample;
		m.Kd = PoolColor(a);		
		m.Shininess = 0.;
	}
	return m;
}

// normal = gradient
vec3 Normal(vec3 a) {
	vec2 e = vec2(.001, 0.);
	float s = Scene(a);
	return normalize(vec3(
		Scene(a+e.xyy) - s,
		Scene(a+e.yxy) - s,
		Scene(a+e.yyx) - s));
}

// shading nearby objects (coefficient)
float Occlusion(vec3 at, vec3 normal) {
	// shading = 0
	float b = 0.;
	// 4 steps
	for (int i = 1; i <= 4; ++i) {
		// .06 - step distance (can be tuned)
		float L = .06 * float(i);
		float d = Scene(at + normal * L);		
		// add difference between the distances traversed and the minimum
		b += max(0., L - d);
	}
	// coefficient <= 1
	return min(b, 1.);
}

vec3 LookAt(vec3 pos, vec3 at, vec3 rDir) {
	vec3 f = normalize(at - pos);
	vec3 r = cross(f, vec3(0., 1., 0.));
	vec3 u = cross(r, f);
	return mat3(r, u, -f) * rDir;
}

// ray tracing function from rPos, rDir - ray direction
float Trace(vec3 rPos, vec3 rDir, float distMin) {
	float L = distMin;
	for (int i = 0; i < MAX_STEPS; ++i) {
		// distance to the nearest object
		float d = Scene(rPos + rDir * L);
		L += d;
		// check threshold value
		if (d < EPSILON * L) break;
	}
	// traveled distance
	return L;
}

vec3 Lighting(vec3 at, vec3 normal, vec3 eye, MaterialInfo m, vec3 lColor, vec3 lPos) {
	// direction to light source from current point
	vec3 lDir = lPos - at;
	
	// throw ray to light source (barriers check)
	vec3 lDirN = normalize(lDir);
	float t = Trace(at, lDirN, EPSILON*2.);
	if (t < length(lDir)) {
		vec3 pos = at + lDirN * t;
		if(!IsWaterSurface(pos))
			return vec3(0.);
	}
	vec3 color = m.Kd * lColor * max(0., dot(normal, normalize(lDir)));
	
	if (m.Shininess > 0.) {
		// Blinn�Phong reflection model (calculates a halfway vector between the viewer and light-source vectors)
		vec3 h = normalize(normalize(lDir) + normalize(eye - at));
		// normalizing in end
		color += lColor * pow(max(0., dot(normal, h)), m.Shininess) * (m.Shininess + 8.) / 25.;
	}
	// divide by square of distance to light source (consequence: additional light spots on water surface)
	return color / dot(lDir, lDir);
}

// rpos - ray position, rdir - ray direction, t - path
vec3 Shade(vec3 rpos, vec3 rdir, float t) {
	// moving end result
	vec3 pos = rpos + rdir * t;
	vec3 nor = Normal(pos);
	
	bool waterSurface = IsWaterSurface(pos);
	bool water = IsWater(pos);
	vec3 waterSurfaceLight = vec3(0);;
	if (waterSurface)
	{
		vec3 refractionDir = refract(normalize(rdir), nor, 0.9);

		waterSurfaceLight = Lighting(pos, nor, rpos, Material(pos), vec3(1.), LightSource);

		// refraction path to pool bottom
		float wt = Trace(pos, refractionDir, 0.03);		
		pos += refractionDir * wt;
		nor = Normal(pos);
	}
	// current point material
	MaterialInfo mat = Material(pos);

	// ambient light
	vec3 color = .11 * (1. - Occlusion(pos, nor)) * mat.Kd;

	// light from light source
	color += Lighting(pos, nor, rpos, mat, vec3(1.), LightSource);
	
	if (water || waterSurface) {
		color *= WaterColor;
		if (waterSurface)
			color += waterSurfaceLight;
	}
	return color;
}

// Ray-generation
vec3 Camera(vec2 px) {
//	vec2 uv = gl_FragCoord.xy / iResolution.xy * 2. - 1.;	
//	uv.x *= iResolution.x / iResolution.y;
//	vec3 rayStart = vec3(3.5, 1.7, 6.);
//	vec3 rayDirection = LookAt(rayStart, vec3(0, -1, 0), normalize(vec3(uv, -2.)));
//	// calculate traveled distance
//	float path = Trace(rayStart, rayDirection, 0.);	
//	return Shade(rayStart, rayDirection, path);

    vec2 uv = gl_FragCoord.xy / iResolution.xy * 2.0 - 1.0;	
    uv.x *= iResolution.x / iResolution.y;
    vec3 rayDirection = LookAt(iCameraPosition, vec3(0, -1, 0), normalize(vec3(uv, -2.)));
    float path = Trace(iCameraPosition, rayDirection, 0.0);	
    return Shade(iCameraPosition, rayDirection, path);
}

void main(void) {
	vec3 col = Camera(gl_FragCoord.xy);
	gl_FragColor = vec4(col, 0.);
}