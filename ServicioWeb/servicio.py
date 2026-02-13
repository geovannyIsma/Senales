from fastapi import FastAPI, HTTPException, Depends
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse, StreamingResponse
from pydantic import BaseModel, Field
from typing import Optional, List
import joblib
import numpy as np
import os
from dotenv import load_dotenv
from datetime import datetime
import json
import csv
import io
from fastapi.staticfiles import StaticFiles

# Importar módulo de base de datos
from database import (
    get_db, init_db, SessionLocal,
    Estudiante, Sesion, IntentoSenal, ErrorDetallado, 
    AjusteDificultad, ConfiguracionEvaluacion
)
from sqlalchemy.orm import Session
from sqlalchemy import func

# Cargar variables de entorno
load_dotenv()

app = FastAPI(
    title="API de Retroalimentación IA y Dificultad Adaptativa",
    description="Genera explicaciones pedagógicas, predice dificultad y gestiona métricas",
    version="2.0.0"
)

# CORS para Unity y Web
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Inicializar base de datos al arrancar
@app.on_event("startup")
def startup_event():
    init_db()
    print("Base de datos inicializada")

# Cargar modelo al iniciar
try:
    model = joblib.load("modelo_dificultad.pkl")
    print("Modelo de dificultad cargado correctamente.")
except Exception as e:
    model = None
    print(f"Advertencia: No se pudo cargar 'modelo_dificultad.pkl': {e}")


# ============== MODELOS PYDANTIC ==============

# Modelos existentes para Dificultad
class DatosJuego(BaseModel):
    zona: int
    senales_mostradas: int
    aciertos: int
    errores: int
    tiempo_promedio: float

class Respuesta(BaseModel):
    dificultad: int
    descripcion: str

# Modelos para IA Generativa
class FeedbackRequest(BaseModel):
    nombre_senal: str
    respuesta_usuario: str
    tiempo_respuesta: float
    nivel_dificultad: int
    zona_actual: int
    intentos_previos: int

class FeedbackResponse(BaseModel):
    success: bool
    significado: str
    motivo_error: str
    ejemplo_real: str
    mnemotecnia: str
    mensaje_completo: str
    error_message: Optional[str] = None

# ===== NUEVOS MODELOS PARA MÉTRICAS =====

class EstudianteCreate(BaseModel):
    nombre: str
    identificador: str

class EstudianteResponse(BaseModel):
    id: int
    nombre: str
    identificador: str
    fecha_registro: datetime
    total_sesiones: int = 0

class SesionCreate(BaseModel):
    estudiante_id: int
    dificultad_inicial: int = 0

class SesionUpdate(BaseModel):
    total_aciertos: int
    total_errores: int
    tiempo_promedio_respuesta: float
    zonas_completadas: int
    zona_maxima_alcanzada: int
    dificultad_final: int
    completada: bool = True

class IntentoCreate(BaseModel):
    sesion_id: int
    nombre_senal: str
    respuesta_usuario: Optional[str] = None
    fue_correcta: bool
    tiempo_respuesta: float
    zona: int = 0
    ronda: int = 0
    dificultad: int = 0

class ErrorCreate(BaseModel):
    sesion_id: int
    nombre_senal: str
    respuesta_usuario: Optional[str] = None
    tipo_error: str  # "confusion", "tiempo_agotado", "distractor"
    tiempo_respuesta: float
    zona: int = 0
    dificultad: int = 0
    intentos_previos: int = 0
    feedback_generado: Optional[str] = None

class AjusteCreate(BaseModel):
    sesion_id: int
    dificultad_anterior: int
    dificultad_nueva: int
    motivo: str
    tasa_aciertos: float
    tiempo_promedio: float
    zona: int = 0
    ronda: int = 0

class ConfiguracionUpdate(BaseModel):
    senales_dificultad_baja: int = Field(ge=1, le=10)
    senales_dificultad_media: int = Field(ge=1, le=15)
    senales_dificultad_alta: int = Field(ge=1, le=20)
    tiempo_dificultad_baja: float = Field(ge=1.0, le=60.0)
    tiempo_dificultad_media: float = Field(ge=1.0, le=30.0)
    tiempo_dificultad_alta: float = Field(ge=1.0, le=20.0)
    dificultad_inicial: int = Field(ge=0, le=2)
    rondas_por_zona: int = Field(ge=1, le=20)
    rondas_minimas_para_completar: int = Field(ge=1, le=10)
    tasa_aciertos_minima: float = Field(ge=0.1, le=1.0)
    usar_modelo_ml: bool = True
    url_servidor_ml: str = "http://127.0.0.1:8000"

class MetricasSesionDetalladas(BaseModel):
    sesion_id: int
    estudiante_nombre: str
    fecha_inicio: datetime
    fecha_fin: Optional[datetime]
    duracion_segundos: float
    total_aciertos: int
    total_errores: int
    tasa_aciertos: float
    tiempo_promedio_respuesta: float
    zonas_completadas: int
    dificultad_inicial: str
    dificultad_final: str
    completada: bool
    datos_suficientes: bool
    
    # Desglose de errores por tipo
    errores_por_tipo: dict
    
    # Tiempos por señal
    tiempos_por_senal: dict
    
    # Historial de ajustes
    ajustes_dificultad: List[dict]
    
    # Lista de intentos
    intentos: List[dict]


# ============== CLIENTE DE IA ==============

class IAClient:
    """Cliente para comunicarse con Google Gemini"""
    
    def __init__(self):
        self.model = os.getenv("AI_MODEL", "gemini-1.5-flash")
        self.api_key = os.getenv("GOOGLE_API_KEY")
        
    async def generar_feedback(self, request: FeedbackRequest) -> FeedbackResponse:
        prompt = self._construir_prompt(request)
        
        try:
            return await self._llamar_google(prompt, request)
        except Exception as e:
            print(f"Error al llamar a IA: {e}")
            return self._generar_fallback(request)
    
    def _construir_prompt(self, request: FeedbackRequest) -> str:
        fue_tiempo_agotado = request.respuesta_usuario == "Tiempo agotado"
        
        if fue_tiempo_agotado:
            contexto_error = f"""- Señal correcta: {request.nombre_senal}
- El estudiante NO respondió a tiempo (se agotó el tiempo)
- Tiempo disponible: {request.tiempo_respuesta:.1f} segundos"""
        else:
            contexto_error = f"""- Señal correcta: {request.nombre_senal}
- Lo que el estudiante respondió: {request.respuesta_usuario}
- Tiempo de respuesta: {request.tiempo_respuesta:.1f} segundos"""
        
        return f"""Eres un instructor de educación vial experto y amigable. Un estudiante está aprendiendo señales de tránsito en un simulador VR y acaba de cometer un error.

CONTEXTO DEL ERROR:
{contexto_error}
- Nivel de dificultad: {['Bajo', 'Medio', 'Alto'][min(request.nivel_dificultad, 2)]}
- Intentos previos con esta señal: {request.intentos_previos}

INSTRUCCIONES:
Genera una respuesta educativa y motivadora con EXACTAMENTE estos 4 elementos (mantenlos breves, máximo 2 oraciones cada uno):

1. SIGNIFICADO: Explica qué significa la señal "{request.nombre_senal}" de forma clara y simple.

2. MOTIVO_ERROR: Explica amablemente por qué pudo haber ocurrido {"que no respondiera a tiempo" if fue_tiempo_agotado else f"la confusión entre '{request.nombre_senal}' y '{request.respuesta_usuario}'"}.

3. EJEMPLO_REAL: Da un ejemplo concreto de una situación de la vida real donde encontrarías esta señal.

4. MNEMOTECNIA: Proporciona un truco o frase memorable para recordar esta señal.

Responde ÚNICAMENTE en formato JSON con esta estructura exacta:
{{
    "significado": "...",
    "motivo_error": "...",
    "ejemplo_real": "...",
    "mnemotecnia": "..."
}}"""

    async def _llamar_google(self, prompt: str, request: FeedbackRequest) -> FeedbackResponse:
        try:
            import google.genai as genai
            import json
            
            if not self.api_key:
                raise Exception("GOOGLE_API_KEY no encontrada")

            client = genai.Client(api_key=self.api_key)
            model_name = self.model if self.model else 'gemini-2.0-flash'
            
            response = client.models.generate_content(
                model=model_name,
                contents=prompt
            )
            
            if not response.text:
                raise Exception("Respuesta de Gemini vacía")

            content = response.text
            
            if "```json" in content:
                content = content.split("```json")[1].split("```")[0]
            elif "```" in content:
                content = content.split("```")[1].split("```")[0]
            
            data = json.loads(content.strip())
            
            return FeedbackResponse(
                success=True,
                significado=data.get("significado", ""),
                motivo_error=data.get("motivo_error", ""),
                ejemplo_real=data.get("ejemplo_real", ""),
                mnemotecnia=data.get("mnemotecnia", ""),
                mensaje_completo=f"{data.get('significado', '')} {data.get('mnemotecnia', '')}"
            )
            
        except Exception as e:
            print(f"--- ERROR EN GEMINI ---")
            print(f"Detalle: {str(e)}")
            return self._generar_fallback(request)

    def _generar_fallback(self, request: FeedbackRequest) -> FeedbackResponse:
        fue_tiempo_agotado = request.respuesta_usuario == "Tiempo agotado"
        
        if fue_tiempo_agotado:
            motivo = "El tiempo de respuesta se agotó. Intenta familiarizarte más con esta señal."
        else:
            motivo = f"Confundiste '{request.nombre_senal}' con '{request.respuesta_usuario}'. "
        
        return FeedbackResponse(
            success=False,
            significado=f"La señal '{request.nombre_senal}' es importante que la conozcas bien.",
            motivo_error=motivo,
            ejemplo_real="Imagina que vas conduciendo y encuentras esta señal.",
            mnemotecnia="Recuerda: cada señal tiene un propósito específico.",
            mensaje_completo=f"La señal '{request.nombre_senal}' es importante. ¡Sigue practicando!",
            error_message="Servicio de IA no disponible."
        )

ia_client = IAClient()


# ============== ENDPOINTS EXISTENTES ==============

@app.get("/")
async def root():
    return {"mensaje": "API Dificultad Adaptativa y Métricas activa", "version": "2.0.0"}

@app.get("/health")
async def health_check():
    return {
        "status": "healthy", 
        "provider": "google", 
        "model": ia_client.model,
        "dificultad_model_loaded": model is not None,
        "database": "sqlite"
    }

@app.post("/predecir", response_model=Respuesta)
def predecir_dificultad(datos: DatosJuego):
    print(f"\n{'='*60}")
    print(f"[ML] SOLICITUD DE PREDICCIÓN RECIBIDA")
    print(f"{'='*60}")
    print(f"[ML] Datos recibidos:")
    print(f"     - Zona: {datos.zona}")
    print(f"     - Señales mostradas: {datos.senales_mostradas}")
    print(f"     - Aciertos: {datos.aciertos}")
    print(f"     - Errores: {datos.errores}")
    print(f"     - Tiempo promedio: {datos.tiempo_promedio:.2f}s")
    
    tasa_aciertos = datos.aciertos / max(datos.senales_mostradas, 1)
    print(f"     - Tasa de aciertos: {tasa_aciertos:.1%}")
    
    if model:
        X = np.array([[
            datos.zona,
            datos.senales_mostradas,
            datos.aciertos,
            datos.errores,
            datos.tiempo_promedio
        ]])
        
        prediccion = int(model.predict(X)[0])
        descripciones = {0: "Baja", 1: "Media", 2: "Alta"}
        descripcion = descripciones.get(prediccion, "Desconocida")
        
        print(f"\n[ML] >>> PREDICCIÓN DEL MODELO: {prediccion} ({descripcion}) <<<")
        print(f"{'='*60}\n")
        
        return Respuesta(
            dificultad=prediccion,
            descripcion=descripcion
        )
    else:
        print("[ML] ⚠️ Modelo no cargado, usando fallback")
        if tasa_aciertos >= 0.8:
            resultado = Respuesta(dificultad=2, descripcion="Alta (Fallback)")
        elif tasa_aciertos >= 0.5:
            resultado = Respuesta(dificultad=1, descripcion="Media (Fallback)")
        else:
            resultado = Respuesta(dificultad=0, descripcion="Baja (Fallback)")
        
        print(f"[ML] >>> PREDICCIÓN FALLBACK: {resultado.dificultad} ({resultado.descripcion}) <<<")
        print(f"{'='*60}\n")
        return resultado

@app.post("/generar_feedback", response_model=FeedbackResponse)
async def generar_feedback(request: FeedbackRequest):
    try:
        response = await ia_client.generar_feedback(request)
        return response
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


# ============== ENDPOINTS DE ESTUDIANTES ==============

@app.post("/estudiantes", response_model=EstudianteResponse)
def crear_estudiante(estudiante: EstudianteCreate, db: Session = Depends(get_db)):
    # Verificar si ya existe
    existente = db.query(Estudiante).filter(
        Estudiante.identificador == estudiante.identificador
    ).first()
    
    if existente:
        raise HTTPException(status_code=400, detail="El identificador ya existe")
    
    nuevo = Estudiante(
        nombre=estudiante.nombre,
        identificador=estudiante.identificador
    )
    db.add(nuevo)
    db.commit()
    db.refresh(nuevo)
    
    return EstudianteResponse(
        id=nuevo.id,
        nombre=nuevo.nombre,
        identificador=nuevo.identificador,
        fecha_registro=nuevo.fecha_registro,
        total_sesiones=0
    )

@app.get("/estudiantes", response_model=List[EstudianteResponse])
def listar_estudiantes(db: Session = Depends(get_db)):
    estudiantes = db.query(Estudiante).all()
    resultado = []
    
    for est in estudiantes:
        total_sesiones = db.query(Sesion).filter(Sesion.estudiante_id == est.id).count()
        resultado.append(EstudianteResponse(
            id=est.id,
            nombre=est.nombre,
            identificador=est.identificador,
            fecha_registro=est.fecha_registro,
            total_sesiones=total_sesiones
        ))
    
    return resultado

@app.get("/estudiantes/{identificador}")
def obtener_estudiante(identificador: str, db: Session = Depends(get_db)):
    estudiante = db.query(Estudiante).filter(
        Estudiante.identificador == identificador
    ).first()
    
    if not estudiante:
        raise HTTPException(status_code=404, detail="Estudiante no encontrado")
    
    sesiones = db.query(Sesion).filter(Sesion.estudiante_id == estudiante.id).all()
    
    return {
        "id": estudiante.id,
        "nombre": estudiante.nombre,
        "identificador": estudiante.identificador,
        "fecha_registro": estudiante.fecha_registro,
        "sesiones": [
            {
                "id": s.id,
                "fecha": s.fecha_inicio,
                "aciertos": s.total_aciertos,
                "errores": s.total_errores,
                "completada": s.completada
            } for s in sesiones
        ]
    }


# ============== ENDPOINTS DE SESIONES ==============

@app.post("/sesiones")
def crear_sesion(sesion: SesionCreate, db: Session = Depends(get_db)):
    # Verificar estudiante
    estudiante = db.query(Estudiante).filter(Estudiante.id == sesion.estudiante_id).first()
    if not estudiante:
        raise HTTPException(status_code=404, detail="Estudiante no encontrado")
    
    nueva = Sesion(
        estudiante_id=sesion.estudiante_id,
        dificultad_inicial=sesion.dificultad_inicial
    )
    db.add(nueva)
    db.commit()
    db.refresh(nueva)
    
    return {"sesion_id": nueva.id, "mensaje": "Sesión creada"}

@app.put("/sesiones/{sesion_id}")
def actualizar_sesion(sesion_id: int, datos: SesionUpdate, db: Session = Depends(get_db)):
    sesion = db.query(Sesion).filter(Sesion.id == sesion_id).first()
    if not sesion:
        raise HTTPException(status_code=404, detail="Sesión no encontrada")
    
    sesion.fecha_fin = datetime.utcnow()
    sesion.duracion_segundos = (sesion.fecha_fin - sesion.fecha_inicio).total_seconds()
    
    # CAMBIO: Calcular totales desde los intentos registrados en la BD
    # en lugar de confiar solo en los valores enviados por Unity
    intentos_bd = db.query(IntentoSenal).filter(IntentoSenal.sesion_id == sesion_id).all()
    
    aciertos_bd = sum(1 for i in intentos_bd if i.fue_correcta)
    errores_bd = sum(1 for i in intentos_bd if not i.fue_correcta)
    total_intentos_bd = len(intentos_bd)
    
    # Calcular tiempo promedio desde la BD
    tiempos = [i.tiempo_respuesta for i in intentos_bd if i.tiempo_respuesta > 0]
    tiempo_promedio_bd = sum(tiempos) / len(tiempos) if tiempos else 0
    
    # Usar los valores de la BD si hay intentos, sino usar los de Unity como fallback
    if total_intentos_bd > 0:
        sesion.total_aciertos = aciertos_bd
        sesion.total_errores = errores_bd
        sesion.tiempo_promedio_respuesta = tiempo_promedio_bd
        print(f"[DEBUG] Usando datos de BD: {aciertos_bd} aciertos, {errores_bd} errores de {total_intentos_bd} intentos")
    else:
        # Fallback a valores de Unity si no hay intentos en BD
        sesion.total_aciertos = datos.total_aciertos
        sesion.total_errores = datos.total_errores
        sesion.tiempo_promedio_respuesta = datos.tiempo_promedio_respuesta
        print(f"[DEBUG] Usando datos de Unity (fallback): {datos.total_aciertos} aciertos, {datos.total_errores} errores")
    
    sesion.zonas_completadas = datos.zonas_completadas
    sesion.zona_maxima_alcanzada = datos.zona_maxima_alcanzada
    sesion.dificultad_final = datos.dificultad_final
    sesion.completada = datos.completada
    
    # Calcular datos_suficientes basado en intentos reales en BD
    total_intentos = sesion.total_aciertos + sesion.total_errores
    sesion.datos_suficientes = total_intentos >= 1
    
    db.commit()
    
    print(f"[DEBUG] Sesión {sesion_id} actualizada - Aciertos: {sesion.total_aciertos}, Errores: {sesion.total_errores}, Datos suficientes: {sesion.datos_suficientes}")
    
    return {
        "mensaje": "Sesión actualizada", 
        "datos_suficientes": sesion.datos_suficientes,
        "total_intentos": total_intentos,
        "intentos_en_bd": total_intentos_bd,  # NUEVO: Para debug
        "aciertos_bd": aciertos_bd,
        "errores_bd": errores_bd
    }

@app.get("/sesiones")
def listar_sesiones(
    estudiante_id: Optional[int] = None,
    completada: Optional[bool] = None,
    limit: int = 50,
    db: Session = Depends(get_db)
):
    query = db.query(Sesion)
    
    if estudiante_id:
        query = query.filter(Sesion.estudiante_id == estudiante_id)
    if completada is not None:
        query = query.filter(Sesion.completada == completada)
    
    sesiones = query.order_by(Sesion.fecha_inicio.desc()).limit(limit).all()
    
    resultado = []
    for s in sesiones:
        estudiante = db.query(Estudiante).filter(Estudiante.id == s.estudiante_id).first()
        resultado.append({
            "id": s.id,
            "estudiante_id": s.estudiante_id,
            "estudiante_nombre": estudiante.nombre if estudiante else "Desconocido",
            "fecha_inicio": s.fecha_inicio,
            "fecha_fin": s.fecha_fin,
            "aciertos": s.total_aciertos,
            "errores": s.total_errores,
            "completada": s.completada,
            "datos_suficientes": s.datos_suficientes
        })
    
    return resultado


# ============== ENDPOINT DE MÉTRICAS DETALLADAS (CASO DE USO 3) ==============

@app.get("/sesiones/{sesion_id}/metricas")
def obtener_metricas_sesion(sesion_id: int, db: Session = Depends(get_db)):
    sesion = db.query(Sesion).filter(Sesion.id == sesion_id).first()
    if not sesion:
        raise HTTPException(status_code=404, detail="Sesión no encontrada")
    
    estudiante = db.query(Estudiante).filter(Estudiante.id == sesion.estudiante_id).first()
    
    # Obtener intentos
    intentos = db.query(IntentoSenal).filter(IntentoSenal.sesion_id == sesion_id).all()
    
    # Obtener errores detallados
    errores = db.query(ErrorDetallado).filter(ErrorDetallado.sesion_id == sesion_id).all()
    
    # Obtener ajustes de dificultad
    ajustes = db.query(AjusteDificultad).filter(AjusteDificultad.sesion_id == sesion_id).all()
    
    # Calcular errores por tipo
    errores_por_tipo = {}
    for e in errores:
        tipo = e.tipo_error
        if tipo not in errores_por_tipo:
            errores_por_tipo[tipo] = {"cantidad": 0, "senales": []}
        errores_por_tipo[tipo]["cantidad"] += 1
        errores_por_tipo[tipo]["senales"].append(e.nombre_senal)
    
    # Calcular tiempos por señal
    tiempos_por_senal = {}
    for i in intentos:
        if i.nombre_senal not in tiempos_por_senal:
            tiempos_por_senal[i.nombre_senal] = {"tiempos": [], "aciertos": 0, "errores": 0}
        tiempos_por_senal[i.nombre_senal]["tiempos"].append(i.tiempo_respuesta)
        if i.fue_correcta:
            tiempos_por_senal[i.nombre_senal]["aciertos"] += 1
        else:
            tiempos_por_senal[i.nombre_senal]["errores"] += 1
    
    # Calcular promedios
    for senal, data in tiempos_por_senal.items():
        data["tiempo_promedio"] = sum(data["tiempos"]) / len(data["tiempos"]) if data["tiempos"] else 0
        del data["tiempos"]  # No enviar lista completa
    
    # Calcular tasa de aciertos
    total = sesion.total_aciertos + sesion.total_errores
    tasa_aciertos = sesion.total_aciertos / total if total > 0 else 0
    
    dificultades = {0: "Baja", 1: "Media", 2: "Alta"}
    
    return {
        "sesion_id": sesion.id,
        "estudiante_nombre": estudiante.nombre if estudiante else "Desconocido",
        "fecha_inicio": sesion.fecha_inicio,
        "fecha_fin": sesion.fecha_fin,
        "duracion_segundos": sesion.duracion_segundos,
        "total_aciertos": sesion.total_aciertos,
        "total_errores": sesion.total_errores,
        "tasa_aciertos": round(tasa_aciertos, 3),
        "tiempo_promedio_respuesta": round(sesion.tiempo_promedio_respuesta, 2),
        "zonas_completadas": sesion.zonas_completadas,
        "dificultad_inicial": dificultades.get(sesion.dificultad_inicial, "Desconocida"),
        "dificultad_final": dificultades.get(sesion.dificultad_final, "Desconocida"),
        "completada": sesion.completada,
        "datos_suficientes": sesion.datos_suficientes,
        "errores_por_tipo": errores_por_tipo,
        "tiempos_por_senal": tiempos_por_senal,
        "ajustes_dificultad": [
            {
                "de": dificultades.get(a.dificultad_anterior, "?"),
                "a": dificultades.get(a.dificultad_nueva, "?"),
                "motivo": a.motivo,
                "tasa_aciertos": a.tasa_aciertos,
                "zona": a.zona,
                "ronda": a.ronda,
                "timestamp": a.timestamp
            } for a in ajustes
        ],
        "intentos": [
            {
                "senal": i.nombre_senal,
                "respuesta": i.respuesta_usuario,
                "correcta": i.fue_correcta,
                "tiempo": i.tiempo_respuesta,
                "zona": i.zona,
                "ronda": i.ronda
            } for i in intentos
        ]
    }


# ============== EXPORTACIÓN DE MÉTRICAS (CASO DE USO 3) ==============

@app.get("/sesiones/{sesion_id}/exportar")
def exportar_metricas(sesion_id: int, formato: str = "json", db: Session = Depends(get_db)):
    sesion = db.query(Sesion).filter(Sesion.id == sesion_id).first()
    if not sesion:
        raise HTTPException(status_code=404, detail="Sesión no encontrada")
    
    # CAMBIO: Permitir exportación con al menos 1 intento
    total_intentos = sesion.total_aciertos + sesion.total_errores
    if total_intentos < 1:
        raise HTTPException(
            status_code=400, 
            detail="No hay intentos registrados en esta sesión."
        )
    
    # Obtener métricas completas
    metricas = obtener_metricas_sesion(sesion_id, db)
    
    if formato.lower() == "csv":
        return exportar_csv(metricas, sesion_id)
    else:
        return exportar_json(metricas, sesion_id)

def exportar_json(metricas: dict, sesion_id: int):
    contenido = json.dumps(metricas, indent=2, default=str, ensure_ascii=False)
    
    return StreamingResponse(
        io.StringIO(contenido),
        media_type="application/json",
        headers={
            "Content-Disposition": f"attachment; filename=metricas_sesion_{sesion_id}.json"
        }
    )

def exportar_csv(metricas: dict, sesion_id: int):
    output = io.StringIO()
    
    # Resumen general
    output.write("=== RESUMEN DE SESIÓN ===\n")
    writer = csv.writer(output)
    writer.writerow(["Métrica", "Valor"])
    writer.writerow(["Sesión ID", metricas["sesion_id"]])
    writer.writerow(["Estudiante", metricas["estudiante_nombre"]])
    writer.writerow(["Fecha Inicio", metricas["fecha_inicio"]])
    writer.writerow(["Duración (s)", metricas["duracion_segundos"]])
    writer.writerow(["Total Aciertos", metricas["total_aciertos"]])
    writer.writerow(["Total Errores", metricas["total_errores"]])
    writer.writerow(["Tasa Aciertos", f"{metricas['tasa_aciertos']:.1%}"])
    writer.writerow(["Tiempo Promedio (s)", metricas["tiempo_promedio_respuesta"]])
    writer.writerow(["Dificultad Inicial", metricas["dificultad_inicial"]])
    writer.writerow(["Dificultad Final", metricas["dificultad_final"]])
    
    # Errores por tipo
    output.write("\n=== ERRORES POR TIPO ===\n")
    writer.writerow(["Tipo Error", "Cantidad", "Señales Afectadas"])
    for tipo, data in metricas["errores_por_tipo"].items():
        writer.writerow([tipo, data["cantidad"], ", ".join(data["senales"])])
    
    # Tiempos por señal
    output.write("\n=== DESEMPEÑO POR SEÑAL ===\n")
    writer.writerow(["Señal", "Tiempo Promedio", "Aciertos", "Errores"])
    for senal, data in metricas["tiempos_por_senal"].items():
        writer.writerow([senal, f"{data['tiempo_promedio']:.2f}", data["aciertos"], data["errores"]])
    
    # Ajustes de dificultad
    output.write("\n=== HISTORIAL DE AJUSTES DE DIFICULTAD ===\n")
    writer.writerow(["De", "A", "Motivo", "Tasa Aciertos", "Zona", "Ronda"])
    for ajuste in metricas["ajustes_dificultad"]:
        writer.writerow([
            ajuste["de"], ajuste["a"], ajuste["motivo"],
            f"{ajuste['tasa_aciertos']:.1%}", ajuste["zona"], ajuste["ronda"]
        ])
    
    # Intentos detallados
    output.write("\n=== INTENTOS DETALLADOS ===\n")
    writer.writerow(["Señal", "Respuesta", "Correcta", "Tiempo (s)", "Zona", "Ronda"])
    for intento in metricas["intentos"]:
        writer.writerow([
            intento["senal"], intento["respuesta"] or "Sin respuesta",
            "Sí" if intento["correcta"] else "No",
            f"{intento['tiempo']:.2f}", intento["zona"], intento["ronda"]
        ])
    
    output.seek(0)
    
    return StreamingResponse(
        output,
        media_type="text/csv",
        headers={
            "Content-Disposition": f"attachment; filename=metricas_sesion_{sesion_id}.csv"
        }
    )


# ============== ENDPOINTS DE REGISTRO (DESDE UNITY) ==============

@app.post("/intentos")
def registrar_intento(intento: IntentoCreate, db: Session = Depends(get_db)):
    # NUEVO: Log de debug
    print(f"[DEBUG] Recibido intento - sesion_id: {intento.sesion_id}, senal: {intento.nombre_senal}, correcta: {intento.fue_correcta}")
    
    # Verificar que la sesión existe
    sesion = db.query(Sesion).filter(Sesion.id == intento.sesion_id).first()
    if not sesion:
        print(f"[ERROR] Sesión {intento.sesion_id} no encontrada")
        raise HTTPException(status_code=404, detail=f"Sesión {intento.sesion_id} no encontrada")
    
    nuevo = IntentoSenal(
        sesion_id=intento.sesion_id,
        nombre_senal=intento.nombre_senal,
        respuesta_usuario=intento.respuesta_usuario,
        fue_correcta=intento.fue_correcta,
        tiempo_respuesta=intento.tiempo_respuesta,
        zona=intento.zona,
        ronda=intento.ronda,
        dificultad=intento.dificultad
    )
    db.add(nuevo)
    db.commit()
    
    print(f"[DEBUG] Intento registrado con ID: {nuevo.id}")
    
    return {"mensaje": "Intento registrado", "id": nuevo.id}

@app.post("/errores")
def registrar_error(error: ErrorCreate, db: Session = Depends(get_db)):
    # NUEVO: Log de debug
    print(f"[DEBUG] Recibido error - sesion_id: {error.sesion_id}, senal: {error.nombre_senal}, tipo: {error.tipo_error}")
    
    # Verificar que la sesión existe
    sesion = db.query(Sesion).filter(Sesion.id == error.sesion_id).first()
    if not sesion:
        print(f"[ERROR] Sesión {error.sesion_id} no encontrada para error")
        raise HTTPException(status_code=404, detail=f"Sesión {error.sesion_id} no encontrada")
    
    nuevo = ErrorDetallado(
        sesion_id=error.sesion_id,
        nombre_senal=error.nombre_senal,
        respuesta_usuario=error.respuesta_usuario,
        tipo_error=error.tipo_error,
        tiempo_respuesta=error.tiempo_respuesta,
        zona=error.zona,
        dificultad=error.dificultad,
        intentos_previos=error.intentos_previos,
        feedback_generado=error.feedback_generado
    )
    db.add(nuevo)
    db.commit()
    
    print(f"[DEBUG] Error registrado con ID: {nuevo.id}")
    
    return {"mensaje": "Error registrado", "id": nuevo.id}

@app.post("/ajustes")
def registrar_ajuste(ajuste: AjusteCreate, db: Session = Depends(get_db)):
    nuevo = AjusteDificultad(
        sesion_id=ajuste.sesion_id,
        dificultad_anterior=ajuste.dificultad_anterior,
        dificultad_nueva=ajuste.dificultad_nueva,
        motivo=ajuste.motivo,
        tasa_aciertos=ajuste.tasa_aciertos,
        tiempo_promedio=ajuste.tiempo_promedio,
        zona=ajuste.zona,
        ronda=ajuste.ronda
    )
    db.add(nuevo)
    db.commit()
    
    return {"mensaje": "Ajuste registrado", "id": nuevo.id}


# ============== ENDPOINTS DE CONFIGURACIÓN (CASO DE USO 4) ==============

@app.get("/configuracion")
def obtener_configuracion(db: Session = Depends(get_db)):
    config = db.query(ConfiguracionEvaluacion).filter(
        ConfiguracionEvaluacion.activa == True
    ).first()
    
    if not config:
        raise HTTPException(status_code=404, detail="No hay configuración activa")
    
    return {
        "id": config.id,
        "nombre": config.nombre_configuracion,
        "senales_dificultad_baja": config.senales_dificultad_baja,
        "senales_dificultad_media": config.senales_dificultad_media,
        "senales_dificultad_alta": config.senales_dificultad_alta,
        "tiempo_dificultad_baja": config.tiempo_dificultad_baja,
        "tiempo_dificultad_media": config.tiempo_dificultad_media,
        "tiempo_dificultad_alta": config.tiempo_dificultad_alta,
        "dificultad_inicial": config.dificultad_inicial,
        "rondas_por_zona": config.rondas_por_zona,
        "rondas_minimas_para_completar": getattr(config, 'rondas_minimas_para_completar', 4),
        "tasa_aciertos_minima": config.tasa_aciertos_minima,
        "usar_modelo_ml": getattr(config, 'usar_modelo_ml', True),
        "url_servidor_ml": getattr(config, 'url_servidor_ml', 'http://127.0.0.1:8000'),
        "fecha_modificacion": config.fecha_modificacion
    }

@app.put("/configuracion")
def actualizar_configuracion(datos: ConfiguracionUpdate, db: Session = Depends(get_db)):
    config = db.query(ConfiguracionEvaluacion).filter(
        ConfiguracionEvaluacion.activa == True
    ).first()
    
    if not config:
        config = ConfiguracionEvaluacion(nombre_configuracion="default", activa=True)
        db.add(config)
    
    # Validaciones adicionales
    errores_validacion = []
    
    if datos.tiempo_dificultad_media >= datos.tiempo_dificultad_baja:
        errores_validacion.append("El tiempo de dificultad media debe ser menor que el de baja")
    
    if datos.tiempo_dificultad_alta >= datos.tiempo_dificultad_media:
        errores_validacion.append("El tiempo de dificultad alta debe ser menor que el de media")
    
    if datos.senales_dificultad_media <= datos.senales_dificultad_baja:
        errores_validacion.append("La cantidad de señales en media debe ser mayor que en baja")
    
    if datos.senales_dificultad_alta <= datos.senales_dificultad_media:
        errores_validacion.append("La cantidad de señales en alta debe ser mayor que en media")
    
    if datos.rondas_minimas_para_completar > datos.rondas_por_zona:
        errores_validacion.append("Las rondas mínimas no pueden ser mayores que las rondas por zona")
    
    if errores_validacion:
        raise HTTPException(
            status_code=400,
            detail={"mensaje": "Parámetros inválidos", "errores": errores_validacion}
        )
    
    # Actualizar valores
    config.senales_dificultad_baja = datos.senales_dificultad_baja
    config.senales_dificultad_media = datos.senales_dificultad_media
    config.senales_dificultad_alta = datos.senales_dificultad_alta
    config.tiempo_dificultad_baja = datos.tiempo_dificultad_baja
    config.tiempo_dificultad_media = datos.tiempo_dificultad_media
    config.tiempo_dificultad_alta = datos.tiempo_dificultad_alta
    config.dificultad_inicial = datos.dificultad_inicial
    config.rondas_por_zona = datos.rondas_por_zona
    config.rondas_minimas_para_completar = datos.rondas_minimas_para_completar
    config.tasa_aciertos_minima = datos.tasa_aciertos_minima
    config.usar_modelo_ml = datos.usar_modelo_ml
    config.url_servidor_ml = datos.url_servidor_ml
    
    db.commit()
    
    return {"mensaje": "Configuración actualizada exitosamente", "fecha": config.fecha_modificacion}


# ============== ESTADÍSTICAS GLOBALES ==============

@app.get("/estadisticas")
def obtener_estadisticas_globales(db: Session = Depends(get_db)):
    total_estudiantes = db.query(Estudiante).count()
    total_sesiones = db.query(Sesion).count()
    sesiones_completadas = db.query(Sesion).filter(Sesion.completada == True).count()
    
    # Promedios
    avg_aciertos = db.query(func.avg(Sesion.total_aciertos)).scalar() or 0
    avg_errores = db.query(func.avg(Sesion.total_errores)).scalar() or 0
    avg_tiempo = db.query(func.avg(Sesion.tiempo_promedio_respuesta)).scalar() or 0
    
    # Errores más comunes
    errores_comunes = db.query(
        ErrorDetallado.nombre_senal,
        func.count(ErrorDetallado.id).label("cantidad")
    ).group_by(ErrorDetallado.nombre_senal).order_by(
        func.count(ErrorDetallado.id).desc()
    ).limit(5).all()
    
    return {
        "total_estudiantes": total_estudiantes,
        "total_sesiones": total_sesiones,
        "sesiones_completadas": sesiones_completadas,
        "tasa_completitud": sesiones_completadas / total_sesiones if total_sesiones > 0 else 0,
        "promedio_aciertos": round(avg_aciertos, 1),
        "promedio_errores": round(avg_errores, 1),
        "promedio_tiempo_respuesta": round(avg_tiempo, 2),
        "errores_mas_comunes": [
            {"senal": e[0], "cantidad": e[1]} for e in errores_comunes
        ]
    }


# ============== ARCHIVOS ESTÁTICOS ==============

# Montar archivos estáticos para el panel web
static_path = os.path.join(os.path.dirname(__file__), "static")
if os.path.exists(static_path):
    app.mount("/panel", StaticFiles(directory=static_path, html=True), name="static")

@app.get("/panel")
async def panel_instructor():
    return FileResponse(os.path.join(static_path, "index.html"))


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
